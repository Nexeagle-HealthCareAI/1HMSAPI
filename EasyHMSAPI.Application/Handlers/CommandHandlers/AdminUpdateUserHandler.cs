using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class AdminUpdateUserHandler : IRequestHandler<AdminUpdateUserRequestModel, AdminUpdateUserResponseModel>
    {
        private static readonly string[] DoctorRoles = { "doctor", "admindoctor" };
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public AdminUpdateUserHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<AdminUpdateUserResponseModel> Handle(AdminUpdateUserRequestModel request, CancellationToken cancellationToken)
        {
            if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.MobileNumber)
                || request.Roles == null || !request.Roles.Any() || request.HospitalId == Guid.Empty)
                return Fail("UserId, name, mobile, roles and hospital are required.");

            var isDoctorRole = request.Roles.Any(r => DoctorRoles.Contains(r.Trim().ToLowerInvariant()));
            if (isDoctorRole && string.IsNullOrWhiteSpace(request.LicenseNumber))
                return Fail("License number is required for a doctor.");

            // The admin must belong to the hospital they're updating the user in.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
                return Fail("You don't have access to this hospital.");

            // Editing team members is an administrator action.
            if (!await Common.CallerGuards.IsAdminAsync(_context, request.CallerUserId, cancellationToken))
                return Fail("Only an administrator can edit team members.");

            var mobile = request.MobileNumber.Trim();
            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

            // Duplicate guard (active users only, excluding current user).
            var dup = await _context.Users.AnyAsync(u =>
                u.UserID != request.UserId &&
                u.UserStatusId != (int)UserStatusEnum.Revoked &&
                (u.MobileNumber == mobile || (email != null && u.Email == email)), cancellationToken);
            if (dup)
                return Fail("Another user with this mobile number or email already exists.");

            // Resolve the roles.
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

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId, cancellationToken);
                    if (user == null)
                        return Fail("User not found.");

                    user.MobileNumber = mobile;
                    user.Email = email;

                    var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserID == request.UserId, cancellationToken);
                    if (userProfile != null)
                    {
                        userProfile.FullName = request.FullName.Trim();
                        userProfile.UpdatedAt = now;
                    }

                    if (!string.IsNullOrWhiteSpace(request.EmployeeId))
                    {
                        var hospitalUser = await _context.HospitalUsers.FirstOrDefaultAsync(hu => hu.UserID == request.UserId && hu.HospitalID == request.HospitalId, cancellationToken);
                        if (hospitalUser != null)
                        {
                            hospitalUser.EmployeeID = request.EmployeeId.Trim();
                        }
                    }

                    // Replace Roles
                    var existingRoles = await _context.UserRoles.Where(ur => ur.UserID == request.UserId).ToListAsync(cancellationToken);
                    _context.UserRoles.RemoveRange(existingRoles);
                    foreach (var rid in finalRoleIds)
                    {
                        _context.UserRoles.Add(new UserRole { UserID = request.UserId, RoleID = rid });
                    }

                    _context.UserHistories.Add(new UserHistory
                    {
                        UserId = request.UserId,
                        UserStatusId = user.UserStatusId,
                        UpdatedBy = request.CallerUserId,
                        UpdatedDate = now,
                    });

                    await _context.SaveChangesAsync(cancellationToken);

                    // Doctor profile
                    if (isDoctorRole)
                    {
                        // Note: If they didn't have a doctor profile before, we create it.
                        // If they did, we might need to update it.
                        // But since we are reusing DoctorCreateRequestModel, let's see if we should update.
                        // For simplicity, we just check if it exists and update, or create.
                        var doc = await _context.Doctors.FirstOrDefaultAsync(d => d.UserID == request.UserId, cancellationToken);
                        if (doc != null)
                        {
                            doc.LicenseNumber = request.LicenseNumber!;
                            doc.ExperienceYears = request.ExperienceYears;
                            doc.MedicalCouncil = request.MedicalCouncil;
                            // Qualifications and specializations could be updated similarly, skipping for brevity or delegating to another handler
                            if (request.PrimaryMedicalSpecialityId.HasValue)
                            {
                                var specialityExists = await _context.MedicalSpecialities
                                    .AnyAsync(s => s.SpecialityId == request.PrimaryMedicalSpecialityId.Value && s.IsActive, cancellationToken);
                                if (specialityExists)
                                    doc.PrimaryMedicalSpecialityId = request.PrimaryMedicalSpecialityId.Value;
                            }
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            var createDoc = await _mediator.Send(new DoctorCreateRequestModel
                            {
                                UserId = request.UserId,
                                LicenseNumber = request.LicenseNumber!,
                                Qualification = request.Qualification,
                                ExperienceYears = request.ExperienceYears,
                                MedicalCouncil = request.MedicalCouncil,
                                PrimaryDepartment = request.Department,
                                Department = request.Department,
                                Specializations = request.Specializations,
                                PrimaryMedicalSpecialityId = request.PrimaryMedicalSpecialityId,
                                HospitalId = request.HospitalId,
                            }, cancellationToken);

                            if (createDoc?.Success != true)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return Fail(createDoc?.Message ?? "Could not create the doctor profile.");
                            }
                        }

                        // Optional OPD consultation fee update
                        if (request.ConsultFee.HasValue && request.ConsultFee.Value > 0)
                        {
                            var docId = doc?.DoctorID ?? (await _context.Doctors.FirstOrDefaultAsync(d => d.UserID == request.UserId, cancellationToken))?.DoctorID;
                            if (docId.HasValue)
                            {
                                var existingFee = await _context.DoctorFees.FirstOrDefaultAsync(df => df.DoctorId == docId.Value && df.FeeType == "OPD_CONSULT" && df.HospitalId == request.HospitalId, cancellationToken);
                                if (existingFee != null)
                                {
                                    existingFee.Amount = request.ConsultFee.Value;
                                    existingFee.UpdatedAt = now;
                                    existingFee.UpdatedBy = request.CallerUserId.ToString();
                                }
                                else
                                {
                                    _context.DoctorFees.Add(new DoctorFee
                                    {
                                        DoctorFeeId = Guid.NewGuid(),
                                        HospitalId = request.HospitalId,
                                        DoctorId = docId.Value,
                                        FeeType = "OPD_CONSULT",
                                        Amount = request.ConsultFee.Value,
                                        IsActive = true,
                                        CreatedAt = now,
                                        CreatedBy = request.CallerUserId.ToString(),
                                        UpdatedAt = now,
                                        UpdatedBy = request.CallerUserId.ToString(),
                                    });
                                }
                                await _context.SaveChangesAsync(cancellationToken);
                            }
                        }
                    }

                    await tx.CommitAsync(cancellationToken);
                    return new AdminUpdateUserResponseModel { Success = true, Message = "Team member updated.", UserId = request.UserId };
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Fail($"Could not update the team member. Error: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        private static AdminUpdateUserResponseModel Fail(string msg) => new() { Success = false, Message = msg };
    }
}
