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
    /// Resets a member's password to a fresh random temporary one and returns it once so the admin
    /// can re-share login details. Reuses the SHA-256(+mask) scheme used by login / Quick Add.
    /// </summary>
    public class ResetCredentialsHandler : IRequestHandler<ResetCredentialsRequestModel, ResetCredentialsResponseModel>
    {
        // Unambiguous character set (no 0/O/1/l/I) for a readable temp password.
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        private readonly AppDbContext _context;
        private readonly IMaskingService _masking;

        public ResetCredentialsHandler(AppDbContext context, IMaskingService masking)
        {
            _context = context;
            _masking = masking;
        }

        public async Task<ResetCredentialsResponseModel> Handle(ResetCredentialsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.UserId == Guid.Empty)
                return Fail("Hospital and user are required.");

            // Resetting your own password here would lock your live session to a temp password.
            if (request.UserId == request.CallerUserId)
                return Fail("You can't reset your own password here — use forgot password instead.");

            // The caller must be an admin who belongs to the hospital.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
                return Fail("You don't have access to this hospital.");
            if (!await Common.CallerGuards.IsAdminAsync(_context, request.CallerUserId, cancellationToken))
                return Fail("Only an administrator can reset a member's password.");

            // The target must be a member of the same hospital.
            var targetMembership = await _context.HospitalUsers
                .Where(hu => hu.UserID == request.UserId && hu.HospitalID == request.HospitalId)
                .Select(hu => new { hu.IsPrimary })
                .FirstOrDefaultAsync(cancellationToken);
            if (targetMembership == null)
                return Fail("This member is not part of your hospital.");
            // Protect the hospital owner — recover their access via forgot password instead.
            if (targetMembership.IsPrimary)
                return Fail("The hospital owner's password can't be reset here.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId, cancellationToken);
            if (user == null)
                return Fail("Member not found.");
            if (user.UserStatusId != (int)UserStatusEnum.Active)
                return Fail("This member is not active.");

            var auth = await _context.UserAuths.FirstOrDefaultAsync(a => a.UserID == request.UserId, cancellationToken);
            if (auth == null)
                return Fail("This member has no login set up.");

            var fullName = await _context.UserProfiles
                .Where(p => p.UserID == request.UserId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            var roleName = await _context.UserRoles
                .Where(ur => ur.UserID == request.UserId)
                .Join(_context.Roles, ur => ur.RoleID, r => r.RoleID, (ur, r) => r.RoleName)
                .FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var tempPassword = GenerateTempPassword();
            auth.HashedPassword = HashPassword(tempPassword);
            auth.PasswordSetAt = now;
            auth.IsLocked = false;
            auth.FailedLoginAttempts = 0;

            // Audit: record who reset whose password and when.
            _context.UserHistories.Add(new UserHistory
            {
                UserId = request.UserId,
                UserStatusId = user.UserStatusId,
                UpdatedBy = request.CallerUserId,
                UpdatedDate = now,
            });

            await _context.SaveChangesAsync(cancellationToken);

            return new ResetCredentialsResponseModel
            {
                Success = true,
                Message = "A new temporary password has been set.",
                TempPassword = tempPassword,
                FullName = fullName,
                MobileNumber = user.MobileNumber,
                Email = user.Email,
                RoleName = roleName,
            };
        }

        private static string GenerateTempPassword(int length = 8)
        {
            var sb = new StringBuilder(length);
            var bytes = RandomNumberGenerator.GetBytes(length);
            foreach (var b in bytes)
                sb.Append(Alphabet[b % Alphabet.Length]);
            return sb.ToString();
        }

        private string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var hex = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            return _masking.IsMaskingEnabled() ? _masking.Mask(hex) : hex;
        }

        private static ResetCredentialsResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
