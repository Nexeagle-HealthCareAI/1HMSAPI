using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Direct admin "quick add": creates an active user with an admin-set password, assigns the role,
    /// adds them to the current hospital, and (for Doctor/AdminDoctor) creates the doctor profile —
    /// all in one transaction. No invitation link / OTP. Reuses the SHA-256(+mask) password scheme
    /// used by login and DoctorCreateHandler for the doctor profile.
    /// </summary>
    public class QuickAddUserHandler : IRequestHandler<QuickAddUserRequestModel, QuickAddUserResponseModel>
    {
        private static readonly string[] DoctorRoles = { "doctor", "admindoctor" };
        private readonly AppDbContext _context;
        private readonly IMaskingService _masking;
        private readonly IMediator _mediator;

        public QuickAddUserHandler(AppDbContext context, IMaskingService masking, IMediator mediator)
        {
            _context = context;
            _masking = masking;
            _mediator = mediator;
        }

        public async Task<QuickAddUserResponseModel> Handle(QuickAddUserRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.MobileNumber)
                || string.IsNullOrWhiteSpace(request.Password) || request.Roles == null || !request.Roles.Any() || request.HospitalId == Guid.Empty)
                return Fail("Name, mobile, password, roles and hospital are required.");

            var isDoctorRole = request.Roles.Any(r => DoctorRoles.Contains(r.Trim().ToLowerInvariant()));
            if (isDoctorRole && string.IsNullOrWhiteSpace(request.LicenseNumber))
                return Fail("License number is required for a doctor.");

            // The admin must belong to the hospital they're adding the user to.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
                return Fail("You don't have access to this hospital.");

            // Adding team members is an administrator action.
            if (!await Common.CallerGuards.IsAdminAsync(_context, request.CallerUserId, cancellationToken))
                return Fail("Only an administrator can add team members.");

            var mobile = request.MobileNumber.Trim();
            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

            // Duplicate guard (active users only).
            var dup = await _context.Users.AnyAsync(u =>
                u.UserStatusId != (int)UserStatusEnum.Revoked &&
                (u.MobileNumber == mobile || (email != null && u.Email == email)), cancellationToken);
            if (dup)
                return Fail("A user with this mobile number or email already exists.");

            // Resolve the roles (prefer a hospital-scoped role, else a system role).
            var roleNames = request.Roles.Select(r => r.Trim().ToLowerInvariant()).ToList();
            var matchedRoles = await _context.Roles
                .Where(r => roleNames.Contains(r.RoleName.ToLower()) && (r.HospitalID == request.HospitalId || r.HospitalID == null))
                .OrderByDescending(r => r.HospitalID != null)
                .Select(r => new { r.RoleName, r.RoleID })
                .ToListAsync(cancellationToken);

            var finalRoleIds = matchedRoles
                .GroupBy(r => r.RoleName.ToLower())
                .Select(g => g.First().RoleID)
                .ToList();

            if (finalRoleIds.Count != roleNames.Count)
                return Fail("One or more roles are not available.");

            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            var employeeId = string.IsNullOrWhiteSpace(request.EmployeeId)
                ? await GenerateNextEmployeeIdAsync(cancellationToken)
                : request.EmployeeId.Trim();

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    _context.Users.Add(new User
                    {
                        UserID = userId,
                        MobileNumber = mobile,
                        Email = email,
                        UserStatusId = (int)UserStatusEnum.Active,
                        CreatedAt = now,
                    });

                    _context.UserAuths.Add(new UserAuth
                    {
                        UserAuthID = Guid.NewGuid(),
                        UserID = userId,
                        UserStatusId = (int)UserStatusEnum.Active,
                        HashedPassword = HashPassword(request.Password),
                        LoginMethod = "PASSWORD",
                        IsLocked = false,
                        PasswordSetAt = now,
                        CreatedAt = now,
                    });

                    foreach (var rid in finalRoleIds)
                    {
                        _context.UserRoles.Add(new UserRole { UserID = userId, RoleID = rid });
                    }

                    _context.UserProfiles.Add(new UserProfile
                    {
                        UserProfileID = Guid.NewGuid(),
                        UserID = userId,
                        UserStatusId = (int)UserStatusEnum.Active,
                        FullName = request.FullName.Trim(),
                        Language = "en-US",
                        EmployeeID = employeeId,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });

                    _context.HospitalUsers.Add(new HospitalUser
                    {
                        HospitalUserID = Guid.NewGuid(),
                        HospitalID = request.HospitalId,
                        UserID = userId,
                        EmployeeID = employeeId,
                        IsPrimary = true,
                        CreatedAt = now,
                    });

                    _context.UserHistories.Add(new UserHistory
                    {
                        UserId = userId,
                        UserStatusId = (int)UserStatusEnum.Active,
                        UpdatedBy = request.CallerUserId,
                        UpdatedDate = now,
                    });

                    await _context.SaveChangesAsync(cancellationToken);

                    // Doctor profile (same DbContext/transaction — atomic with the user).
                    if (isDoctorRole)
                    {
                        var doc = await _mediator.Send(new DoctorCreateRequestModel
                        {
                            UserId = userId,
                            LicenseNumber = request.LicenseNumber!,
                            Qualification = request.Qualification,
                            ExperienceYears = request.ExperienceYears,
                            MedicalCouncil = request.MedicalCouncil,
                            PrimaryDepartment = request.Department,   // mark the chosen department as primary too
                            Department = request.Department,
                            Specializations = request.Specializations,
                            HospitalId = request.HospitalId,
                        }, cancellationToken);

                        if (doc?.Success != true)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return Fail(doc?.Message ?? "Could not create the doctor profile.");
                        }

                        // Optional OPD consultation fee — set it now so the doctor doesn't bill ₹0.
                        if (request.ConsultFee.HasValue && request.ConsultFee.Value > 0 && doc.DoctorId.HasValue)
                        {
                            _context.DoctorFees.Add(new DoctorFee
                            {
                                DoctorFeeId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                DoctorId = doc.DoctorId.Value,
                                FeeType = "OPD_CONSULT",
                                Amount = request.ConsultFee.Value,
                                IsActive = true,
                                CreatedAt = now,
                                CreatedBy = request.CallerUserId.ToString(),
                                UpdatedAt = now,
                                UpdatedBy = request.CallerUserId.ToString(),
                            });
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                    }

                    await tx.CommitAsync(cancellationToken);
                    return new QuickAddUserResponseModel { Success = true, Message = "Team member added.", UserId = userId };
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Fail($"Could not add the team member. Error: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        private string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var hex = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            return _masking.IsMaskingEnabled() ? _masking.Mask(hex) : hex;
        }

        private async Task<string> GenerateNextEmployeeIdAsync(CancellationToken cancellationToken)
        {
            const string prefix = "EMP";
            var existing = await _context.UserProfiles
                .Where(u => u.EmployeeID != null && u.EmployeeID.StartsWith(prefix))
                .Select(u => u.EmployeeID!)
                .ToListAsync(cancellationToken);
            int max = 0;
            foreach (var eid in existing)
                if (int.TryParse(eid.Substring(prefix.Length), out var n) && n > max) max = n;
            return prefix + (max + 1);
        }

        private static QuickAddUserResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
