using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;

        public PublicBookAppointmentHandler(AppDbContext context, ISmsService smsService, IWhatsAppMessagingService whatsAppMessagingService)
        {
            _context = context;
            _smsService = smsService;
            _whatsAppMessagingService = whatsAppMessagingService;
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

            var isReminderSent = false;
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

                try
                {
                    // Same "appointment confirmation" WhatsApp template ConfirmPreAppointmentHandler
                    // sends later — reused here for the immediate NexEagle booking-success moment too.
                    // No real token exists yet (that's only allocated at staff confirmation), so it's
                    // left blank here, and the date/time shown is the patient's PREFERRED slot, not a
                    // locked one — the hospital may still adjust it, same caveat the SMS above states.
                    var hospitalName = await _context.Hospitals
                        .Where(h => h.HospitalID == hospitalId)
                        .Select(h => h.Name)
                        .FirstOrDefaultAsync(cancellationToken);
                    var doctorName = await _context.Doctors
                        .Where(d => d.DoctorID == request.DoctorId)
                        .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName)
                        .FirstOrDefaultAsync(cancellationToken);

                    isReminderSent = await _whatsAppMessagingService.SendAppointmentConfirmationAsync(
                        patient.Mobile,
                        patient.FullName ?? string.Empty,
                        hospitalName ?? string.Empty,
                        doctorName ?? string.Empty,
                        string.Empty,
                        appointment.ApptDate.ToString("dd-MM-yyyy"),
                        appointment.StartAt.ToString("HH:mm"));
                }
                catch
                {
                    // Best-effort — never fail the booking because WhatsApp delivery threw.
                }
            }

            return new PublicBookAppointmentResponseModel
            {
                Success = true,
                Message = "Your appointment request has been received. Our team will confirm your slot shortly.",
                AppointmentId = appointment.ApptId,
                PatientId = patient.PatientId,
                IsReminderSent = isReminderSent,
            };
        }
    }
}
