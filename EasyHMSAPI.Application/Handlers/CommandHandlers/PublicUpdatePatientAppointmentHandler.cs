using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Anonymous/bot-facing patient-detail correction — the other half of the "update" action in
    // the WhatsApp "check my appointment" flow, alongside PublicUpdateDoctorAppointmentHandler.
    // Same AppointmentId-as-secret + Mobile gate as the sibling public appointment handlers.
    //
    // IMPORTANT: PatientRegistration is a shared record — one row can be (and by design is)
    // referenced by every appointment that patient has ever had (see
    // AppointmentBookingHelpers.FindOrCreatePatientAsync, which mutates the same row in place at
    // booking time). There is no per-appointment snapshot of patient demographics anywhere in the
    // schema. A correction made here is therefore visible on this patient's ENTIRE visit history
    // at this hospital, not just the one appointment in the URL — that's accepted as the intended
    // behavior (this endpoint edits the patient's identity record, using the appointment purely as
    // the auth anchor), not a bug to work around.
    //
    // Per-field update semantics deliberately mirror FindOrCreatePatientAsync's own
    // "only overwrite if a non-empty value was supplied" idiom for these same columns, so a
    // caller correcting just one field can't accidentally blank out the others.
    public class PublicUpdatePatientAppointmentHandler : IRequestHandler<PublicUpdatePatientAppointmentRequestModel, PublicUpdatePatientAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<PublicUpdatePatientAppointmentHandler> _logger;

        public PublicUpdatePatientAppointmentHandler(AppDbContext context, ISmsService smsService, ILogger<PublicUpdatePatientAppointmentHandler> logger)
        {
            _context = context;
            _smsService = smsService;
            _logger = logger;
        }

        private static string NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            else if (digits.Length == 11 && digits.StartsWith("0")) digits = digits[1..];
            return digits;
        }

        public async Task<PublicUpdatePatientAppointmentResponseModel> Handle(PublicUpdatePatientAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId, cancellationToken);

                if (appt == null)
                    return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                var patient = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(p => p.PatientId == appt.PatientId, cancellationToken);

                if (patient == null || NormalizeMobile(patient.Mobile) != NormalizeMobile(request.Mobile) || NormalizeMobile(request.Mobile) == string.Empty)
                    return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                    return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "This appointment was cancelled — book a new appointment instead." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
                    return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "Cannot update patient details on a completed appointment." };

                var fields = request.Patient;
                var hasFullName = !string.IsNullOrWhiteSpace(fields.FullName);
                var hasGender = !string.IsNullOrWhiteSpace(fields.Gender);
                var hasGuardian = !string.IsNullOrWhiteSpace(fields.Guardian);
                if (!hasFullName && !fields.Age.HasValue && !hasGender && !hasGuardian)
                    return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "At least one field is required." };

                if (fields.Age.HasValue && (fields.Age.Value < 0 || fields.Age.Value > 130))
                    return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "Age must be between 0 and 130." };

                if (hasFullName) patient.FullName = fields.FullName;
                if (fields.Age.HasValue) patient.Age = fields.Age.Value;
                if (hasGender) patient.Sex = fields.Gender;
                if (hasGuardian) patient.GuardianName = fields.Guardian;

                await _context.SaveChangesAsync(cancellationToken);

                var hospitalName = await _context.Hospitals
                    .Where(h => h.HospitalID == appt.HospitalId)
                    .Select(h => h.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                var doctorName = await _context.Doctors
                    .Where(d => d.DoctorID == appt.DoctorId)
                    .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
                doctorName ??= "Doctor";

                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your details for the appointment on {appt.ApptDate:yyyy-MM-dd} have been updated.";
                    var isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                    _logger.LogInformation("Public-updated patient details on appointment {AppointmentId}, SMS sent: {IsSmsSent}", appt.ApptId, isSmsSent);
                }

                return new PublicUpdatePatientAppointmentResponseModel
                {
                    Success = true,
                    Message = "Patient details updated.",
                    Appointment = new PublicAppointmentSummary
                    {
                        AppointmentId = appt.ApptId,
                        DoctorName = doctorName,
                        HospitalName = hospitalName ?? "Hospital",
                        ApptDate = appt.ApptDate,
                        StartAt = appt.StartAt,
                        Status = PublicAppointmentStatusLabels.ToPatientLabel(appt.CurrentStatusCode),
                        StatusCode = appt.CurrentStatusCode ?? string.Empty,
                    },
                    Patient = new PublicPatientSummary
                    {
                        FullName = patient.FullName ?? string.Empty,
                        Age = patient.Age,
                        Gender = patient.Sex,
                        Guardian = patient.GuardianName,
                    },
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient details on appointment {AppointmentId} via public endpoint", request.AppointmentId);
                return new PublicUpdatePatientAppointmentResponseModel { Success = false, Message = "An error occurred while updating patient details." };
            }
        }
    }
}
