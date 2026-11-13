using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// The WhatsApp messages around a Doctor Dekho visit (Vita vision, Phase 4 "After booking"), in the language the patient booked in:
    ///
    ///   confirmation   right after booking: doctor, hospital, when, booking ID, fee (pay at the hospital), landmark
    ///   morning        on the morning of the visit (from 07:30 India time)
    ///   soon           about two hours before the time, when the booking has one
    ///   after          two hours after the doctor marks the visit complete: rate the doctor, book the follow-up the doctor asked for
    ///   leave          at the time the patient said they would set off (Vita "takes you there"), with the navigation link
    ///
    /// Each template has two URL buttons (base URL fixed in Meta, the end filled per message): "Start navigation"
    /// (https://www.google.com/maps/dir/?api=1&amp;destination={{1}}) and "Visit details" (Doctor Dekho /appointments/visit/{{1}}, where the
    /// patient finds the hospital's visit guide, checks in and sees their token). "after" has only "Visit details".
    ///
    /// Template names carry the language: visit_confirmation_en / _hi / _hinglish (Hinglish templates are written in Roman Hindi and
    /// registered under language "en"). Times are the hospital's wall-clock (India) time, as StartAt is stored.
    /// </summary>
    public static class VisitMessages
    {
        public const string Confirmation = "visit_confirmation", Morning = "visit_reminder_morning", Soon = "visit_reminder_soon", After = "visit_after";
        /// <summary>"Time to leave" at the leave time the patient gave Vita (AppointmentJourneys.PlannedLeaveAt). Same variables and
        /// buttons as the reminders: patient_name, doctor_name, hospital_name, when, directions; Start navigation + Visit details.</summary>
        public const string Leave = "visit_leave_now";
        public static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);
        public static readonly TimeSpan MorningAt = new(7, 30, 0);
        public static readonly TimeSpan SoonBefore = TimeSpan.FromHours(2);
        public static readonly TimeSpan AfterDelay = TimeSpan.FromHours(2);
        // No after-visit message at night: it waits until the morning.
        public static readonly TimeSpan QuietUntil = new(8, 0, 0), QuietFrom = new(21, 30, 0);
        public const string DefaultSiteUrl = "https://doctordekho.nexeagle.com";

        /// <summary>"hi", "hindi" -> "hi"; "hinglish", "hg" -> "hinglish"; anything else (or nothing) -> "en". Null input -> null.</summary>
        public static string? CleanLanguage(string? value)
        {
            var v = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Length == 0) return null;
            return v is "hi" or "hindi" ? "hi" : v is "hinglish" or "hg" ? "hinglish" : "en";
        }

        /// <summary>(template name, Meta language code) for a message in the patient's language.</summary>
        public static (string Name, string Code) Template(string kind, string? language) => CleanLanguage(language) switch
        {
            "hi" => ($"{kind}_hi", "hi"),
            "hinglish" => ($"{kind}_hinglish", "en"),
            _ => ($"{kind}_en", "en"),
        };

        public static DateTime ToIst(DateTime utc) => utc + IstOffset;

        /// <summary>The visit has a real time (a booking made without one is stored at midnight).</summary>
        public static bool HasTime(Appointment a) => a.StartAt.TimeOfDay != TimeSpan.Zero;

        public static bool IsOpen(Appointment a) =>
            a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled && a.CurrentStatusCode != AppConstants.AppointmentStatus_Completed;

        /// <summary>Which reminder or follow-up message is due for this appointment now, or null. "skip:..." means: mark it as handled
        /// without sending (the moment has passed, or the patient booked so late that the confirmation already said it all).</summary>
        public static string? Due(Appointment a, DateTime nowUtc)
        {
            if (a.BookingSource != AppConstants.BookingSource_NexeaglePublic) return null;
            var now = ToIst(nowUtc);
            var bookedAt = ToIst(a.CreatedAt);

            if (a.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
            {
                if (a.AfterVisitSentAt != null) return null;
                if (nowUtc - a.LastStatusCodeAt > TimeSpan.FromDays(2)) return "skip:" + After;
                if (nowUtc - a.LastStatusCodeAt < AfterDelay) return null;
                return now.TimeOfDay >= QuietUntil && now.TimeOfDay < QuietFrom ? After : null;
            }
            if (!IsOpen(a)) return null;

            if (a.MorningReminderSentAt == null && a.ApptDate.Date == now.Date && now.TimeOfDay >= MorningAt)
            {
                if (bookedAt.Date == now.Date || (HasTime(a) && a.StartAt <= now)) return "skip:" + Morning;
                return Morning;
            }
            if (a.BeforeVisitReminderSentAt == null && HasTime(a) && now >= a.StartAt - SoonBefore)
            {
                if (now >= a.StartAt || bookedAt >= a.StartAt - SoonBefore) return "skip:" + Soon;
                return Soon;
            }
            return null;
        }

        public static void MarkHandled(Appointment a, string kind, DateTime nowUtc)
        {
            switch (kind)
            {
                case Confirmation: a.ConfirmationSentAt = nowUtc; break;
                case Morning: a.MorningReminderSentAt = nowUtc; break;
                case Soon: a.BeforeVisitReminderSentAt = nowUtc; break;
                case After: a.AfterVisitSentAt = nowUtc; break;
            }
        }

        /// <summary>Everything a message about one appointment needs, read in one go.</summary>
        public sealed class Context
        {
            public string PatientName = "";
            public string? Mobile;
            public string DoctorName = "Doctor";
            public string HospitalName = "Hospital";
            public string? City;
            public decimal? Latitude, Longitude;
            public decimal? Fee;
            public HospitalVisitGuide? Guide;
            public DateTime? FollowUpDate;
        }

        public static async Task<Context> LoadAsync(AppDbContext db, Appointment a, CancellationToken ct)
        {
            var c = new Context();
            var patient = await db.PatientRegistrations.AsNoTracking()
                .Where(p => p.PatientId == a.PatientId && p.HospitalId == a.HospitalId)
                .Select(p => new { p.FullName, p.Mobile }).FirstOrDefaultAsync(ct);
            c.PatientName = patient?.FullName ?? "";
            c.Mobile = patient?.Mobile;
            var hospital = await db.Hospitals.AsNoTracking().Where(h => h.HospitalID == a.HospitalId)
                .Select(h => new { h.Name, h.City, h.Latitude, h.Longitude }).FirstOrDefaultAsync(ct);
            c.HospitalName = hospital?.Name ?? "Hospital";
            c.City = hospital?.City;
            c.Latitude = hospital?.Latitude;
            c.Longitude = hospital?.Longitude;
            c.DoctorName = await db.Doctors.AsNoTracking().Where(d => d.DoctorID == a.DoctorId)
                .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName).FirstOrDefaultAsync(ct) ?? "Doctor";
            c.Fee = await db.DoctorFees.AsNoTracking()
                .Where(f => f.DoctorId == a.DoctorId && f.HospitalId == a.HospitalId && f.FeeType == "OPD_CONSULT" && f.IsActive)
                .Select(f => (decimal?)f.Amount).FirstOrDefaultAsync(ct);
            c.Guide = await db.HospitalVisitGuides.AsNoTracking().FirstOrDefaultAsync(g => g.HospitalId == a.HospitalId, ct);
            c.FollowUpDate = await db.Prescription.AsNoTracking().Where(p => p.ApptId == a.ApptId && p.FollowUpDate != null)
                .Select(p => p.FollowUpDate).FirstOrDefaultAsync(ct);
            return c;
        }

        public static string Reference(Guid appointmentId) => appointmentId.ToString("N")[..8].ToUpperInvariant();

        /// <summary>The end of the "Start navigation" link: the hospital's pin, or its name and city when it has none.</summary>
        public static string Destination(Context c) =>
            c.Latitude != null && c.Longitude != null
                ? FormattableString.Invariant($"{c.Latitude:0.######},{c.Longitude:0.######}")
                : Uri.EscapeDataString(string.Join(", ", new[] { c.HospitalName, c.City }.Where(x => !string.IsNullOrWhiteSpace(x))));

        public static string When(Appointment a) => a.StartAt.ToString(HasTime(a) ? "ddd d MMM, HH:mm" : "ddd d MMM", System.Globalization.CultureInfo.InvariantCulture);

        public static string Directions(Context c) =>
            string.Join(". ", new[] { c.Guide?.Landmark, c.Guide?.EntranceNote, c.Guide?.OpdLocation }.Where(x => !string.IsNullOrWhiteSpace(x)));

        /// <summary>The named body variables for each template (all are filled; an empty value is sent as "-").</summary>
        public static List<KeyValuePair<string, string>> Parameters(string kind, Appointment a, Context c)
        {
            var p = new List<KeyValuePair<string, string>>
            {
                new("patient_name", string.IsNullOrWhiteSpace(c.PatientName) ? "there" : c.PatientName),
                new("doctor_name", c.DoctorName),
            };
            if (kind == After) return p;
            p.Add(new("hospital_name", c.HospitalName));
            p.Add(new("when", When(a)));
            if (kind == Confirmation)
            {
                p.Add(new("booking_id", Reference(a.ApptId)));
                p.Add(new("fee", c.Fee != null ? FormattableString.Invariant($"Rs {c.Fee:0}") : "-"));
            }
            p.Add(new("directions", Directions(c)));
            return p;
        }

        public static string[] Buttons(string kind, Appointment a, Context c) =>
            kind == After ? new[] { a.ApptId.ToString() } : new[] { Destination(c), a.ApptId.ToString() };

        /// <summary>Sends one message for this appointment. Returns WhatsApp's answer (Refused when there is no number).</summary>
        public static async Task<WhatsAppSendResult> SendAsync(IWhatsAppMessagingService whatsApp, string kind, Appointment a, Context c)
        {
            if (string.IsNullOrWhiteSpace(c.Mobile)) return WhatsAppSendResult.Refused;
            var (name, code) = Template(kind, a.PatientLanguage);
            return await whatsApp.SendTemplateWithButtonsAsync(c.Mobile, name, code, Parameters(kind, a, c), Buttons(kind, a, c));
        }
    }
}
