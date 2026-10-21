using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Saves the Doctor Dekho app's quick-booking preferences. Only the fields sent are changed; the shared account profile is untouched.</summary>
    public class SavePatientAppProfileHandler : IRequestHandler<SavePatientAppProfileRequestModel, PatientAppProfileResponseModel>
    {
        public static readonly string[] Languages = { "en", "hi", "bn", "hg" };
        public static readonly string[] TimeWindows = { "MORNING", "AFTERNOON", "EVENING", "NIGHT" };

        private readonly AppDbContext _context;

        public SavePatientAppProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientAppProfileResponseModel> Handle(SavePatientAppProfileRequestModel r, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile))
                return Fail(401, "Please log in again.");

            var language = PatientProfileRules.Clean(r.PreferredLanguage)?.ToLowerInvariant();
            if (language != null && !Languages.Contains(language)) return Fail(400, "Choose English, Hindi, Bengali or Hinglish.");

            var window = PatientProfileRules.Clean(r.PreferredTimeWindow)?.ToUpperInvariant();
            if (window != null && !TimeWindows.Contains(window)) return Fail(400, "Choose morning, afternoon, evening or night.");

            if (r.HomeLatitude is < -90m or > 90m || r.HomeLongitude is < -180m or > 180m) return Fail(400, "That location is not valid.");
            if ((r.HomeLatitude == null) != (r.HomeLongitude == null)) return Fail(400, "Send both latitude and longitude.");

            var city = PatientProfileRules.Clean(r.HomeCity);
            var state = PatientProfileRules.Clean(r.HomeState);
            if (city?.Length > 100 || state?.Length > 100) return Fail(400, "City or state is too long.");

            // A default "book for" person must be one of this account's own family members.
            if (r.DefaultMemberId.HasValue && r.ClearDefaultMember != true
                && !await _context.PatientFamilyMembers.AnyAsync(m => m.MemberId == r.DefaultMemberId.Value && m.OwnerMobile == r.Mobile, ct))
                return Fail(404, "Family member not found.");

            var now = DateTime.UtcNow;
            var row = await _context.PatientAppProfiles.FirstOrDefaultAsync(p => p.Mobile == r.Mobile, ct);
            if (row == null)
            {
                row = new PatientAppProfile { Mobile = r.Mobile, CreatedAt = now };
                _context.PatientAppProfiles.Add(row);
            }

            if (language != null) row.PreferredLanguage = language;
            if (city != null) row.HomeCity = city;
            if (state != null) row.HomeState = state;
            if (r.HomeLatitude.HasValue) { row.HomeLatitude = Math.Round(r.HomeLatitude.Value, 6); row.HomeLongitude = Math.Round(r.HomeLongitude!.Value, 6); }
            if (r.ClearDefaultMember == true) row.DefaultMemberId = null;
            else if (r.DefaultMemberId.HasValue) row.DefaultMemberId = r.DefaultMemberId;
            if (window != null) row.PreferredTimeWindow = window;
            if (r.LastDoctorId.HasValue) { row.LastDoctorId = r.LastDoctorId; row.LastBookedAt = now; }
            if (r.RemindersOptIn.HasValue && r.RemindersOptIn.Value != row.RemindersOptIn)
            {
                row.RemindersOptIn = r.RemindersOptIn.Value;
                row.RemindersOptInAt = now; // when the choice was last made, in either direction
            }
            if (r.OnboardingCompleted == true && row.OnboardingCompletedAt == null) row.OnboardingCompletedAt = now;
            row.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);

            var saved = GetPatientAppProfileHandler.From(row, await GetPatientAppProfileHandler.HasShortProfileAsync(_context, r.Mobile, ct));
            saved.Message = "Saved.";
            return saved;
        }

        private static PatientAppProfileResponseModel Fail(int status, string message) =>
            new() { Success = false, StatusCode = status, Message = message };
    }

    /// <summary>Registers or refreshes one install of the app for the signed-in mobile (upsert on Mobile + InstallationId).</summary>
    public class RegisterPatientAppDeviceHandler : IRequestHandler<RegisterPatientAppDeviceRequestModel, PatientAppDeviceResponseModel>
    {
        private readonly AppDbContext _context;

        public RegisterPatientAppDeviceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientAppDeviceResponseModel> Handle(RegisterPatientAppDeviceRequestModel r, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile))
                return new PatientAppDeviceResponseModel { Success = false, StatusCode = 401, Message = "Please log in again." };

            var installationId = PatientProfileRules.Clean(r.InstallationId);
            var platform = PatientProfileRules.Clean(r.Platform)?.ToLowerInvariant();
            if (installationId == null || installationId.Length > 64)
                return new PatientAppDeviceResponseModel { Success = false, StatusCode = 400, Message = "A valid installation id is required." };
            if (platform is not ("android" or "ios"))
                return new PatientAppDeviceResponseModel { Success = false, StatusCode = 400, Message = "Platform must be android or ios." };

            var pushToken = PatientProfileRules.Clean(r.PushToken);
            if (pushToken?.Length > 256)
                return new PatientAppDeviceResponseModel { Success = false, StatusCode = 400, Message = "Push token is too long." };

            var now = DateTime.UtcNow;
            var row = await _context.PatientAppDevices.FirstOrDefaultAsync(d => d.Mobile == r.Mobile && d.InstallationId == installationId, ct);
            if (row == null)
            {
                row = new PatientAppDevice { DeviceId = Guid.NewGuid(), Mobile = r.Mobile, InstallationId = installationId, FirstSeenAt = now };
                _context.PatientAppDevices.Add(row);
            }
            row.Platform = platform;
            row.AppVersion = Truncate(PatientProfileRules.Clean(r.AppVersion), 20);
            row.OsVersion = Truncate(PatientProfileRules.Clean(r.OsVersion), 40);
            row.DeviceModel = Truncate(PatientProfileRules.Clean(r.DeviceModel), 100);
            if (pushToken != null) row.PushToken = pushToken;
            row.IsActive = true;
            row.LastSeenAt = now;

            // A push token belongs to one phone: if another account signed in on this phone before, stop sending to it there.
            if (pushToken != null)
            {
                var others = await _context.PatientAppDevices
                    .Where(d => d.PushToken == pushToken && d.IsActive && !(d.Mobile == r.Mobile && d.InstallationId == installationId))
                    .ToListAsync(ct);
                foreach (var other in others) other.IsActive = false;
            }

            await _context.SaveChangesAsync(ct);
            return new PatientAppDeviceResponseModel { Success = true, Message = "Saved.", DeviceId = row.DeviceId };
        }

        private static string? Truncate(string? v, int max) => v == null || v.Length <= max ? v : v[..max];
    }

    /// <summary>Switches an install off on sign-out. Unknown installs succeed quietly so sign-out never fails.</summary>
    public class DeactivatePatientAppDeviceHandler : IRequestHandler<DeactivatePatientAppDeviceRequestModel, PatientAppDeviceResponseModel>
    {
        private readonly AppDbContext _context;

        public DeactivatePatientAppDeviceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientAppDeviceResponseModel> Handle(DeactivatePatientAppDeviceRequestModel r, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile))
                return new PatientAppDeviceResponseModel { Success = false, StatusCode = 401, Message = "Please log in again." };

            var row = await _context.PatientAppDevices.FirstOrDefaultAsync(d => d.Mobile == r.Mobile && d.InstallationId == r.InstallationId, ct);
            if (row != null && row.IsActive)
            {
                row.IsActive = false;
                row.LastSeenAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
            return new PatientAppDeviceResponseModel { Success = true, Message = "Signed out on this device.", DeviceId = row?.DeviceId };
        }
    }
}
