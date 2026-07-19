using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Authenticated "my appointments" — Mobile here is always controller-supplied from an
    // OTP-verified JWT claim (see PatientAuthController/PatientTokenValidator), never a raw query
    // param, which is what makes this safe to query by phone number at all (unlike the guest
    // ID-only lookup, this deliberately returns EVERYTHING for that number). Looks across every
    // hospital's PatientRegistrations for this mobile, not just one — a patient can have separate
    // registration rows per hospital they've visited.
    public class GetPublicAppointmentsByMobileHandler : IRequestHandler<GetPublicAppointmentsByMobileRequestModel, GetPublicAppointmentsByMobileResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicAppointmentsByMobileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicAppointmentsByMobileResponseModel> Handle(GetPublicAppointmentsByMobileRequestModel request, CancellationToken cancellationToken)
        {
            // Fully read-only, and doubles as the "am I logged in" check on effectively every page
            // load of the patient portal. See GetPublicDoctorsHandler for why this matters at volume.
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var patientIds = await _context.PatientRegistrations
                .Where(p => p.Mobile == request.Mobile && p.PatientId != null)
                .Select(p => p.PatientId!)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (patientIds.Count == 0)
            {
                return new GetPublicAppointmentsByMobileResponseModel { Success = true, Mobile = request.Mobile, Appointments = new() };
            }

            var appts = await _context.Appointments
                .Where(a => a.PatientId != null && patientIds.Contains(a.PatientId))
                .OrderByDescending(a => a.ApptDate).ThenByDescending(a => a.StartAt)
                .Select(a => new { a.ApptId, a.HospitalId, a.DoctorId, a.ApptDate, a.StartAt, a.CurrentStatusCode })
                .ToListAsync(cancellationToken);

            var hospitalIds = appts.Select(a => a.HospitalId).Distinct().ToList();
            var doctorIds = appts.Select(a => a.DoctorId).Distinct().ToList();

            var hospitalNames = await _context.Hospitals
                .Where(h => hospitalIds.Contains(h.HospitalID))
                .ToDictionaryAsync(h => h.HospitalID, h => h.Name, cancellationToken);
            var doctorNames = await _context.Doctors
                .Where(d => doctorIds.Contains(d.DoctorID))
                .Select(d => new { d.DoctorID, Name = d.User.UserProfiles.FirstOrDefault()!.FullName })
                .ToDictionaryAsync(d => d.DoctorID, d => d.Name, cancellationToken);

            var summaries = appts.Select(a => new PublicAppointmentSummary
            {
                AppointmentId = a.ApptId,
                DoctorName = doctorNames.TryGetValue(a.DoctorId, out var dn) ? dn ?? "Doctor" : "Doctor",
                HospitalName = hospitalNames.TryGetValue(a.HospitalId, out var hn) ? hn ?? "Hospital" : "Hospital",
                ApptDate = a.ApptDate,
                StartAt = a.StartAt,
                Status = PublicAppointmentStatusLabels.ToPatientLabel(a.CurrentStatusCode),
                StatusCode = a.CurrentStatusCode ?? string.Empty,
            }).ToList();

            return new GetPublicAppointmentsByMobileResponseModel { Success = true, Mobile = request.Mobile, Appointments = summaries };
        }
    }
}
