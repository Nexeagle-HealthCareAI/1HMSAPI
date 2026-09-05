using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Net;

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
    /// Also fires NotifyDoctorAsync (WhatsApp + email + in-app Alert) at the moment the request
    /// lands, so the doctor knows the instant an online request comes in rather than only when
    /// front-desk later confirms it.
    /// </summary>
    public class PublicBookAppointmentHandler : IRequestHandler<PublicBookAppointmentRequestModel, PublicBookAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public PublicBookAppointmentHandler(AppDbContext context, ISmsService smsService, IWhatsAppMessagingService whatsAppMessagingService, IEmailService emailService, IMemoryCache cache, IConfiguration configuration)
        {
            _context = context;
            _smsService = smsService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _emailService = emailService;
            _cache = cache;
            _configuration = configuration;
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
                BookedByMobile = request.VerifiedMobile,
                Reason = request.Reason ?? string.Empty,
                StatusHistoryJson = $"[{{\"status\":\"{AppConstants.AppointmentStatus_PreAppointment}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]",
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = null,
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync(cancellationToken);

            // The new PRE_APPOINTMENT row occupies its preferred time in DoctorBookedSlotsHandler's
            // query (it isn't Cancelled, so nothing there excludes pre-appointments) — without this,
            // a cached "booked slots" entry could keep showing that time as open for up to its TTL.
            _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(hospitalId, request.DoctorId, appointment.ApptDate));

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

            await NotifyDoctorAsync(appointment, hospitalId, request.DoctorId, patient, cancellationToken);

            return new PublicBookAppointmentResponseModel
            {
                Success = true,
                Message = "Your appointment request has been received. Our team will confirm your slot shortly.",
                AppointmentId = appointment.ApptId,
                PatientId = patient.PatientId,
                IsReminderSent = isReminderSent,
            };
        }

        // Alerts the treating doctor AND every Admin/AdminDoctor at the hospital across all three
        // channels the instant an online (Doctor Dekho) request lands — WhatsApp/email never expose
        // the patient's full number (only the hospital's own staff, confirming the appointment, need
        // that), and the in-app Alert reuses the same bell/notification-center pipeline every other
        // module already dispatches through. Every channel here is best-effort: none of them can
        // fail the booking that already succeeded.
        private async Task NotifyDoctorAsync(Appointment appointment, Guid hospitalId, Guid doctorId, PatientRegistration patient, CancellationToken cancellationToken)
        {
            var doctorInfo = await _context.Doctors
                .Where(d => d.DoctorID == doctorId)
                .Select(d => new
                {
                    d.User.UserID,
                    d.User.MobileNumber,
                    d.User.Email,
                    DoctorName = d.User.UserProfiles.FirstOrDefault()!.FullName,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (doctorInfo == null)
                return;

            var hospitalName = await _context.Hospitals
                .Where(h => h.HospitalID == hospitalId)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var patientName = patient.FullName ?? "A patient";
            var patientAddress = string.IsNullOrWhiteSpace(patient.AddressLine) ? "Not provided" : patient.AddressLine;
            var maskedMobile = MaskMobile(patient.Mobile);
            var webAppBaseUrl = (_configuration["WebApp:BaseUrl"] ?? "https://1hms.nexeagle.com").TrimEnd('/');
            var loginUrl = $"{webAppBaseUrl}/appointment-dashboard";
            var treatingDoctorName = doctorInfo.DoctorName ?? "Doctor";

            // Admin/AdminDoctor users at this hospital — same role names + case-insensitive match
            // CallerGuards.IsAdminAsync uses. Excludes the treating doctor themselves (they're
            // notified above regardless of whether they also hold an Admin/AdminDoctor role) so
            // nobody gets the same alert twice.
            var adminRoleNames = new[] { "admin", "admindoctor" };
            var admins = await (from hu in _context.HospitalUsers.AsNoTracking()
                                 join u in _context.Users.AsNoTracking() on hu.UserID equals u.UserID
                                 join ur in _context.UserRoles.AsNoTracking() on u.UserID equals ur.UserID
                                 join r in _context.Roles.AsNoTracking() on ur.RoleID equals r.RoleID
                                 where hu.HospitalID == hospitalId
                                       && (r.HospitalID == hospitalId || r.IsSystemDefined)
                                       && u.UserID != doctorInfo.UserID
                                 select new { u.UserID, u.MobileNumber, u.Email, RoleName = r.RoleName }
                                ).ToListAsync(cancellationToken);

            var adminUsers = admins
                .Where(a => !string.IsNullOrWhiteSpace(a.RoleName) && adminRoleNames.Contains(a.RoleName.Trim().ToLowerInvariant()))
                .GroupBy(a => a.UserID)
                .Select(g => g.First())
                .ToList();

            await SendAppointmentAlertAsync(doctorInfo.MobileNumber, doctorInfo.Email, treatingDoctorName, isTreatingDoctor: true, treatingDoctorName, patientName, maskedMobile, patientAddress, hospitalName, loginUrl);
            foreach (var admin in adminUsers)
            {
                await SendAppointmentAlertAsync(admin.MobileNumber, admin.Email, "there", isTreatingDoctor: false, treatingDoctorName, patientName, maskedMobile, patientAddress, hospitalName, loginUrl);
            }

            try
            {
                var now = DateTime.UtcNow;
                var body = $"{patientName} ({maskedMobile}) requested an appointment with Dr. {treatingDoctorName} via Doctor Dekho.";
                _context.Alert.Add(new Alert
                {
                    AlertId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    AlertCode = "ONLINE_APPOINTMENT_REQUEST",
                    Severity = "INFO",
                    Title = "New online appointment request",
                    Body = body,
                    PatientId = patient.PatientId,
                    AudienceUserId = doctorInfo.UserID,
                    Status = "ACTIVE",
                    RaisedAt = now,
                    SourceModule = "PublicBookAppointment",
                    SourceRefId = appointment.ApptId.ToString(),
                    DispatchInApp = true,
                    CreatedAt = now,
                });
                // Separate role-targeted row (rather than one row per admin) — same AudienceRoles
                // convention every other alert-raising handler uses for a group audience.
                _context.Alert.Add(new Alert
                {
                    AlertId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    AlertCode = "ONLINE_APPOINTMENT_REQUEST",
                    Severity = "INFO",
                    Title = "New online appointment request",
                    Body = body,
                    PatientId = patient.PatientId,
                    AudienceRoles = "Admin,AdminDoctor",
                    Status = "ACTIVE",
                    RaisedAt = now,
                    SourceModule = "PublicBookAppointment",
                    SourceRefId = appointment.ApptId.ToString(),
                    DispatchInApp = true,
                    CreatedAt = now,
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Best-effort — never fail the booking because the in-app alert insert threw.
            }
        }

        // One recipient's WhatsApp + email send, shared by the treating doctor and every hospital
        // admin — the only difference is the greeting ("Dr. {name}" vs a plain "there") and whether
        // the WhatsApp template's doctor_name slot describes the recipient themselves or just gives
        // an admin recipient context on which doctor the request is for.
        private async Task SendAppointmentAlertAsync(string? mobileNumber, string? email, string greetingName, bool isTreatingDoctor, string treatingDoctorName, string patientName, string maskedMobile, string patientAddress, string hospitalName, string loginUrl)
        {
            if (!string.IsNullOrWhiteSpace(mobileNumber))
            {
                try
                {
                    await _whatsAppMessagingService.SendDoctorNewOnlineAppointmentAlertAsync(
                        mobileNumber,
                        treatingDoctorName,
                        patientName,
                        maskedMobile,
                        patientAddress,
                        loginUrl);
                }
                catch
                {
                    // Best-effort — never fail the booking because WhatsApp delivery threw.
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    var html = BuildDoctorEmailBody(greetingName, isTreatingDoctor, treatingDoctorName, patientName, maskedMobile, patientAddress, hospitalName, loginUrl);
                    await _emailService.SendInvitationEmailAsync(email, "New online appointment request", html);
                }
                catch
                {
                    // Best-effort — never fail the booking because the SMTP provider is down.
                }
            }
        }

        // Keeps only the last 4 digits visible — same convention WhatsAppMessagingService.MaskMobile
        // uses for logs, applied here to the actual notification content per the "hide contact"
        // requirement (doctors get enough to recognize a repeat caller, not the dialable number).
        private static string MaskMobile(string? mobile)
        {
            if (string.IsNullOrEmpty(mobile) || mobile.Length <= 4)
                return "****";
            return new string('*', mobile.Length - 4) + mobile[^4..];
        }

        private static string BuildDoctorEmailBody(string greetingName, bool isTreatingDoctor, string treatingDoctorName, string patientName, string maskedMobile, string patientAddress, string hospitalName, string loginUrl)
        {
            var hospitalLine = string.IsNullOrWhiteSpace(hospitalName)
                ? string.Empty
                : $"<p style='font-size:14px;color:#555;margin:0 0 16px;'>Hospital: <strong>{WebUtility.HtmlEncode(hospitalName)}</strong></p>";

            var greeting = isTreatingDoctor ? $"Hi Dr. {WebUtility.HtmlEncode(greetingName)}," : $"Hi {WebUtility.HtmlEncode(greetingName)},";
            var intro = isTreatingDoctor
                ? "A patient just requested an appointment with you via Doctor Dekho."
                : $"A patient just requested an appointment with Dr. {WebUtility.HtmlEncode(treatingDoctorName)} via Doctor Dekho.";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background-color: #f8f9fa; padding: 24px; border-radius: 8px;'>
                        <h2 style='color: #4f46e5; margin: 0 0 16px;'>New Online Appointment Request</h2>
                        <p style='font-size: 15px; color: #333; margin: 0 0 8px;'>{greeting}</p>
                        <p style='font-size: 15px; color: #333; margin: 0 0 16px;'>{intro}</p>
                        {hospitalLine}
                        <table style='border-collapse: collapse; margin: 8px 0 20px; width: 100%;'>
                            <tr>
                                <td style='padding:8px 16px; background:#eef2ff; color:#3730a3; font-weight:bold; border-radius:6px 0 0 0;'>Patient</td>
                                <td style='padding:8px 16px; background:#ffffff; color:#111; border:1px solid #eef2ff;'>{WebUtility.HtmlEncode(patientName)}</td>
                            </tr>
                            <tr>
                                <td style='padding:8px 16px; background:#eef2ff; color:#3730a3; font-weight:bold;'>Contact</td>
                                <td style='padding:8px 16px; background:#ffffff; color:#111; border:1px solid #eef2ff;'>{WebUtility.HtmlEncode(maskedMobile)} (full number visible to hospital staff)</td>
                            </tr>
                            <tr>
                                <td style='padding:8px 16px; background:#eef2ff; color:#3730a3; font-weight:bold; border-radius:0 0 0 6px;'>Address</td>
                                <td style='padding:8px 16px; background:#ffffff; color:#111; border:1px solid #eef2ff;'>{WebUtility.HtmlEncode(patientAddress)}</td>
                            </tr>
                        </table>
                        <p style='margin:0 0 20px;'>
                            <a href='{WebUtility.HtmlEncode(loginUrl)}' style='background:#4f46e5;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold;'>Log in to view appointment</a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                        <p style='font-size: 12px; color: #999; margin: 0;'>This is an automated message. Please do not reply to this email.</p>
                    </div>
                </div>";
        }
    }
}
