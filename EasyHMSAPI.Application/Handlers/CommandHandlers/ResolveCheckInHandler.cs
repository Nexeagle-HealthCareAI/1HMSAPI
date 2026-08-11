using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Walk-in OPD QR check-in: the patient's phone number is all the bot has (no known
    // AppointmentId), so this resolves "their appointment today at this hospital" -- but only
    // after a geofence check passes. Gating the mobile lookup behind the geofence check (rather
    // than after it, or not at all) means nobody can probe "does mobile X have an appointment
    // today" without first being physically at the hospital -- this endpoint is anonymous
    // (PublicController), so that gate is the only thing standing between this and a phone-number
    // enumeration oracle. Contrast with GetPublicAppointmentsByMobileHandler, which is safe to
    // query by raw mobile only because its Mobile always comes from an OTP-verified JWT claim,
    // never a client-supplied value like this one.
    public class ResolveCheckInHandler : IRequestHandler<ResolveCheckInRequestModel, ResolveCheckInResponseModel>
    {
        private readonly AppDbContext _context;

        public ResolveCheckInHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ResolveCheckInResponseModel> Handle(ResolveCheckInRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.Mobile))
                return new ResolveCheckInResponseModel { Success = false, Message = "HospitalId and Mobile are required." };

            var (geofenceOk, geofenceError) = await QueueCheckInHelper.CheckGeofenceAsync(
                _context, request.HospitalId, request.Latitude, request.Longitude, cancellationToken);
            if (!geofenceOk)
                return new ResolveCheckInResponseModel { Success = false, Message = geofenceError };

            var today = DateTime.UtcNow.Date;

            var patientIds = await _context.PatientRegistrations
                .Where(p => p.Mobile == request.Mobile && p.HospitalId == request.HospitalId && p.PatientId != null)
                .Select(p => p.PatientId!)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (patientIds.Count == 0)
                return new ResolveCheckInResponseModel { Success = false, Message = "No appointment found for today." };

            var appts = await _context.Appointments
                .Where(a => a.HospitalId == request.HospitalId
                    && a.PatientId != null && patientIds.Contains(a.PatientId)
                    && a.ApptDate.Date == today
                    && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled)
                .OrderBy(a => a.StartAt)
                .Select(a => new { a.ApptId, a.DoctorId, a.StartAt })
                .ToListAsync(cancellationToken);

            if (appts.Count == 0)
                return new ResolveCheckInResponseModel { Success = false, Message = "No appointment found for today." };

            if (appts.Count == 1)
            {
                var match = appts[0];
                var checkIn = await QueueCheckInHelper.CheckInAsync(
                    _context, match.ApptId, AppConstants.QueueArrivalMethod_Geofence,
                    requireGeofence: false, request.Latitude, request.Longitude, cancellationToken);

                return new ResolveCheckInResponseModel
                {
                    Success = checkIn.Success,
                    Message = checkIn.Message,
                    AppointmentId = checkIn.Success ? match.ApptId : null,
                    TokenNo = checkIn.TokenNo,
                    Status = checkIn.Status,
                };
            }

            var doctorIds = appts.Select(a => a.DoctorId).Distinct().ToList();
            var doctorNames = await _context.Doctors
                .Where(d => doctorIds.Contains(d.DoctorID))
                .Select(d => new { d.DoctorID, Name = d.User.UserProfiles.FirstOrDefault()!.FullName })
                .ToDictionaryAsync(d => d.DoctorID, d => d.Name, cancellationToken);

            return new ResolveCheckInResponseModel
            {
                Success = false,
                Message = "Multiple appointments found for today. Please choose one.",
                Candidates = appts.Select(a => new CheckInCandidate
                {
                    AppointmentId = a.ApptId,
                    DoctorName = doctorNames.TryGetValue(a.DoctorId, out var dn) ? dn ?? "Doctor" : "Doctor",
                    StartAt = a.StartAt,
                }).ToList(),
            };
        }
    }
}
