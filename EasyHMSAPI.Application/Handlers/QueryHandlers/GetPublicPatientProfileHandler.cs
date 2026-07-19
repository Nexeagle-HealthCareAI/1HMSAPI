using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Read-only "my details" for the Doctor Dekho profile page. A single mobile number can have a
    // PatientRegistrations row per hospital they've visited (see GetPublicAppointmentsByMobileHandler)
    // — there's no single authoritative one, so this picks the most recently created row as the
    // best-effort "current" answer rather than trying to merge/reconcile fields across hospitals.
    // Deliberately read-only: writing back would mean picking which hospital's row to edit, which
    // is exactly the ambiguity this sidesteps — patient-detail corrections stay a front-desk action.
    public class GetPublicPatientProfileHandler : IRequestHandler<GetPublicPatientProfileRequestModel, GetPublicPatientProfileResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicPatientProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicPatientProfileResponseModel> Handle(GetPublicPatientProfileRequestModel request, CancellationToken cancellationToken)
        {
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var patient = await _context.PatientRegistrations
                .Where(p => p.Mobile == request.Mobile)
                .OrderByDescending(p => p.RegisteredAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (patient == null)
            {
                // Not an error — a number that logged in but has never actually completed a
                // booking yet (e.g. OTP-verified, then browsing before their first appointment).
                return new GetPublicPatientProfileResponseModel { Success = true, Message = "No details on file yet." };
            }

            return new GetPublicPatientProfileResponseModel
            {
                Success = true,
                FullName = patient.FullName,
                Age = patient.Age,
                AgeUnit = patient.AgeUnit,
                Sex = patient.Sex,
                Email = patient.Email,
                GuardianName = patient.GuardianName,
                GuardianRelation = patient.GuardianRelation,
            };
        }
    }
}
