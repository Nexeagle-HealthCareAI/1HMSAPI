using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Public (Nexeagle) booking handler — deliberately separate from RegisterAppointmentHandler
    /// rather than reusing it (that handler hardcodes a staff-JWT UserId and route-authenticated
    /// HospitalId, neither of which a public caller has). Creates a PRE_APPOINTMENT row that
    /// claims no real time slot and allocates no token — the receptionist assigns the actual
    /// StartAt and token later via ConfirmPreAppointmentHandler. Reuses the same patient-matching
    /// logic RegisterAppointmentHandler uses (via AppointmentBookingHelpers) so a visitor who's
    /// already a hospital patient — matched by mobile+name — doesn't get a duplicate record.
    /// HospitalId is resolved from DoctorId via PublicDirectoryHelpers (gated on both
    /// Hospital.IsPubliclyListed and Doctor.IsPubliclyListed) — never client-supplied, same
    /// reasoning GetPublicDoctorAvailabilityHandler uses.
    /// </summary>
    public class PublicBookAppointmentHandler : IRequestHandler<PublicBookAppointmentRequestModel, PublicBookAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        public PublicBookAppointmentHandler(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<PublicBookAppointmentResponseModel> Handle(PublicBookAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new PublicBookAppointmentResponseModel { Success = false, Message = "DoctorId is required." };

            var doctorHospitalId = await PublicDirectoryHelpers.ResolvePubliclyListedHospitalIdAsync(_context, request.DoctorId, cancellationToken);

            if (doctorHospitalId == null)
                return new PublicBookAppointmentResponseModel { Success = false, Message = "Doctor not found." };

            var hospitalId = doctorHospitalId.Value;

            PatientRegistration patient;
            try
            {
                patient = await AppointmentBookingHelpers.FindOrCreatePatientAsync(_context, request.Patient, hospitalId, null, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                return new PublicBookAppointmentResponseModel { Success = false, Message = ex.Message };
            }

            if (patient.PatientId is null)
                return new PublicBookAppointmentResponseModel { Success = false, Message = "Could not resolve patient." };

            // Non-binding placeholder: nothing is validated/reserved against this time — the real
            // slot is chosen and locked in when the receptionist confirms this pre-appointment.
            var preferredDate = request.PreferredDate.Date;
            var preferredStart = preferredDate.Add(request.PreferredTime ?? TimeSpan.Zero);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = request.DoctorId,
                PatientId = patient.PatientId,
                ApptDate = preferredDate,
                StartAt = preferredStart,
                EndAt = preferredStart.AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_PreAppointment,
                BookingSource = AppConstants.BookingSource_NexeaglePublic,
                BookingIpAddress = request.IpAddress,
                BookingReferrerUrl = request.ReferrerUrl,
                BookingUtmCampaign = request.UtmCampaign,
                Reason = request.Reason ?? string.Empty,
                StatusHistoryJson = $"[{{\"status\":\"{AppConstants.AppointmentStatus_PreAppointment}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]",
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = null,
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(patient.Mobile))
            {
                try
                {
                    var msg = $"Dear {patient.FullName}, your appointment request has been received. Our team will confirm your slot shortly.";
                    await _smsService.SendInvitationSmsAsync(patient.Mobile, msg);
                }
                catch
                {
                    // Best-effort — never fail the booking because the SMS provider is down.
                }
            }

            return new PublicBookAppointmentResponseModel
            {
                Success = true,
                Message = "Your appointment request has been received. Our team will confirm your slot shortly.",
                AppointmentId = appointment.ApptId,
                PatientId = patient.PatientId,
            };
        }
    }
}
