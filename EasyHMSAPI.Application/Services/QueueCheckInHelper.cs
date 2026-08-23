using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EasyHMSAPI.Application.Services
{
    // Shared core of "convert a booked appointment into a queue token" -- used by both the
    // patient-facing geofence-checked self-check-in (IssueQueueTokenHandler) and the staff-facing
    // reception override (MarkArrivedHandler, no geofence). Idempotent: a retried call for an
    // appointment that already has a token just returns the existing one, never double-allocates.
    public static class QueueCheckInHelper
    {
        // Shared by CheckInAsync (below, appointment-first) and ResolveCheckInHandler
        // (hospital-first, before it ever touches PatientRegistrations) -- extracted so a mobile
        // lookup can be geofence-gated up front rather than duplicating this check.
        public static async Task<(bool Ok, string? ErrorMessage)> CheckGeofenceAsync(
            AppDbContext context,
            Guid hospitalId,
            decimal? patientLatitude,
            decimal? patientLongitude,
            CancellationToken cancellationToken)
        {
            var hospital = await context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == hospitalId, cancellationToken);
            if (hospital == null)
                return (false, "Hospital not found.");

            if (!hospital.Latitude.HasValue || !hospital.Longitude.HasValue)
                return (false, "This hospital hasn't set up location-based check-in yet. Please check in at reception.");

            if (!patientLatitude.HasValue || !patientLongitude.HasValue)
                return (false, "Location is required to check in.");

            var distanceMeters = GeofenceHelper.DistanceMeters(patientLatitude.Value, patientLongitude.Value, hospital.Latitude.Value, hospital.Longitude.Value);
            if (distanceMeters > AppConstants.GeofenceRadiusMeters)
                return (false, "You don't appear to be at the hospital yet. Please check in at reception if this seems wrong.");

            return (true, null);
        }

        public static async Task<IssueQueueTokenResponseModel> CheckInAsync(
            AppDbContext context,
            Guid appointmentId,
            string arrivalMethod,
            bool requireGeofence,
            decimal? patientLatitude,
            decimal? patientLongitude,
            CancellationToken cancellationToken)
        {
            var appointment = await context.Appointments.FirstOrDefaultAsync(a => a.ApptId == appointmentId, cancellationToken);
            if (appointment == null)
                return new IssueQueueTokenResponseModel { Success = false, Message = "Appointment not found." };

            if (appointment.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                return new IssueQueueTokenResponseModel { Success = false, Message = "This appointment has been cancelled." };

            var existingToken = await context.AppointmentTokens.FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId, cancellationToken);
            if (existingToken != null)
                return new IssueQueueTokenResponseModel { Success = true, TokenNo = existingToken.TokenNo, Status = existingToken.Status, Message = "Already checked in." };

            if (requireGeofence)
            {
                var (ok, errorMessage) = await CheckGeofenceAsync(context, appointment.HospitalId, patientLatitude, patientLongitude, cancellationToken);
                if (!ok)
                    return new IssueQueueTokenResponseModel { Success = false, Message = errorMessage };
            }

            var tokenNo = await AppointmentBookingHelpers.AllocateTokenWithLockingAsync(
                context, appointment.HospitalId, appointment.DoctorId, appointment.ApptDate, appointment.ApptId, cancellationToken);
            if (!tokenNo.HasValue)
                return new IssueQueueTokenResponseModel { Success = false, Message = "Could not issue a queue token right now. Please try again." };

            var token = await context.AppointmentTokens.FirstAsync(t => t.ApptId == appointment.ApptId, cancellationToken);
            token.Status = AppConstants.QueueTokenStatus_Waiting;
            token.ArrivedAt = DateTime.UtcNow;
            token.ArrivalMethod = arrivalMethod;
            token.ArrivalLatitude = patientLatitude;
            token.ArrivalLongitude = patientLongitude;

            // QueueSequence is the single ordering key both Call and Skip read (never Appointment.
            // StartAt directly at read time) -- so the "hybrid rule" has to be applied here, once,
            // at the moment a patient joins the live waiting queue: slot the newly-checked-in
            // patient in among the current WAITING list by slot time, not by check-in order, so an
            // earlier slot checking in after a later one still queues ahead of it. From here on,
            // QueueSequence is authoritative; a later skip repositions purely by queue position.
            var waitingQueue = await (
                from t2 in context.AppointmentTokens
                join a2 in context.Appointments on t2.ApptId equals a2.ApptId
                where t2.HospitalId == appointment.HospitalId && t2.DoctorId == appointment.DoctorId && t2.TokenDate == token.TokenDate
                   && t2.Status == AppConstants.QueueTokenStatus_Waiting && t2.TokenId != token.TokenId
                orderby t2.QueueSequence
                select new { Token = t2, a2.StartAt }
            ).ToListAsync(cancellationToken);

            var insertIndex = waitingQueue.FindIndex(x => x.StartAt > appointment.StartAt);
            if (insertIndex < 0) insertIndex = waitingQueue.Count;

            var finalOrder = waitingQueue.Select(x => x.Token).ToList();
            finalOrder.Insert(insertIndex, token);
            for (var i = 0; i < finalOrder.Count; i++)
            {
                finalOrder[i].QueueSequence = i + 1;
            }

            await context.SaveChangesAsync(cancellationToken);

            return new IssueQueueTokenResponseModel { Success = true, TokenNo = token.TokenNo, Status = token.Status };
        }
    }
}
