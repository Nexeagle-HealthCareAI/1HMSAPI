using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>The Doctor Dekho app's quick-booking preferences, plus whether the shared short profile is complete enough to book with.
    /// A patient who has never saved anything gets defaults (Success = true), never a 404, so the app can open straight away.</summary>
    public class GetPatientAppProfileHandler : IRequestHandler<GetPatientAppProfileRequestModel, PatientAppProfileResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientAppProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientAppProfileResponseModel> Handle(GetPatientAppProfileRequestModel request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Mobile))
                return new PatientAppProfileResponseModel { Success = false, StatusCode = 401, Message = "Please log in again." };

            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            var row = await _context.PatientAppProfiles.FirstOrDefaultAsync(p => p.Mobile == request.Mobile, ct);
            var hasShortProfile = await HasShortProfileAsync(_context, request.Mobile, ct);
            return From(row, hasShortProfile);
        }

        internal static async Task<bool> HasShortProfileAsync(AppDbContext context, string mobile, CancellationToken ct) =>
            await context.PatientAccountProfiles.AnyAsync(p => p.Mobile == mobile
                && ((p.FirstName != null && p.FirstName != "") || p.FullName != "")
                && p.Age != null && p.Sex != null, ct);

        internal static PatientAppProfileResponseModel From(PatientAppProfile? row, bool hasShortProfile) => row == null
            ? new PatientAppProfileResponseModel { Success = true, HasShortProfile = hasShortProfile }
            : new PatientAppProfileResponseModel
            {
                Success = true,
                PreferredLanguage = row.PreferredLanguage,
                HomeCity = row.HomeCity,
                HomeState = row.HomeState,
                HomeLatitude = row.HomeLatitude,
                HomeLongitude = row.HomeLongitude,
                DefaultMemberId = row.DefaultMemberId,
                PreferredTimeWindow = row.PreferredTimeWindow,
                LastDoctorId = row.LastDoctorId,
                LastBookedAt = row.LastBookedAt,
                RemindersOptIn = row.RemindersOptIn,
                OnboardingCompleted = row.OnboardingCompletedAt != null,
                HasShortProfile = hasShortProfile,
            };
    }
}
