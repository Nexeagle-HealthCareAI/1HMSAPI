using System.Globalization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// The patient's trip to the clinic (Vita "takes you there"). Rules shared by the public journey endpoints, the visit page, the
    /// "time to leave" WhatsApp and the reception board:
    ///
    ///   modes      drive | auto_cab | transit | walk
    ///   statuses   planned -> on_the_way -> (late) -> arrived; cancelled when the patient drops it
    ///   leave time on the appointment day or the day before, not after the appointment's own time (hospital wall-clock)
    ///   ETA        minutes from now, 0-600; distance left 0-2000 km; late by 0-600 minutes
    ///   reception  sees a journey only when the patient said yes (ShareWithHospital), and never a location
    /// </summary>
    public static class Journeys
    {
        public static readonly string[] Modes = { "drive", "auto_cab", "transit", "walk" };
        public const string Planned = "planned", OnTheWay = "on_the_way", Late = "late", Arrived = "arrived", Cancelled = "cancelled";
        public static readonly string[] Progress = { OnTheWay, Late, Arrived, Cancelled };
        /// <summary>The "time to leave" WhatsApp is sent from the leave time until this long after it; later it is marked and skipped.</summary>
        public static readonly TimeSpan LeaveWindow = TimeSpan.FromMinutes(30);

        public static string? CleanMode(string? mode)
        {
            var m = (mode ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            m = m switch { "car" or "bike" or "own" or "driving" => "drive", "auto" or "cab" or "taxi" => "auto_cab",
                           "bus" or "train" or "metro" or "public" => "transit", "walking" or "foot" => "walk", _ => m };
            return Modes.Contains(m) ? m : null;
        }

        /// <summary>"08:15" (on the appointment day) or "2026-10-26T08:15" -> hospital wall-clock time; null when it can't be read.</summary>
        public static DateTime? ParseLeave(string? value, Appointment a)
        {
            var v = (value ?? "").Trim();
            if (v.Length == 0) return null;
            if (TimeSpan.TryParseExact(v, new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out var t))
                return a.ApptDate.Date.Add(t);
            if (DateTime.TryParseExact(v, new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d))
                return d;
            return null;
        }

        /// <summary>Why this leave time can't be used, or null when it's fine.</summary>
        public static string? LeaveProblem(DateTime leave, Appointment a)
        {
            if (leave.Date != a.ApptDate.Date && leave.Date != a.ApptDate.Date.AddDays(-1))
                return "The leave time must be on the day of the appointment or the day before.";
            if (VisitMessages.HasTime(a) && leave >= a.StartAt)
                return "The leave time must be before the appointment.";
            return null;
        }

        public static bool IsPublic(Appointment a) => a.BookingSource == AppConstants.BookingSource_NexeaglePublic;

        public static PublicJourney? ToPublic(AppointmentJourney? j, DateTime nowUtc)
        {
            if (j == null) return null;
            return new PublicJourney
            {
                TravelMode = j.TravelMode,
                PlannedLeaveAt = j.PlannedLeaveAt?.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                ShareWithHospital = j.ShareWithHospital,
                Status = j.Status,
                StartedAt = j.StartedAt,
                EtaAt = j.EtaAt,
                EtaMinutes = EtaMinutes(j, nowUtc),
                DistanceKm = j.DistanceKm,
                LateByMinutes = j.LateByMinutes,
                ArrivedAt = j.ArrivedAt,
                LeaveReminderSent = j.LeaveReminderSentAt != null,
            };
        }

        public static int? EtaMinutes(AppointmentJourney j, DateTime nowUtc) =>
            j.EtaAt == null || (j.Status != OnTheWay && j.Status != Late) ? null : Math.Max(0, (int)Math.Ceiling((j.EtaAt.Value - nowUtc).TotalMinutes));
    }
}
