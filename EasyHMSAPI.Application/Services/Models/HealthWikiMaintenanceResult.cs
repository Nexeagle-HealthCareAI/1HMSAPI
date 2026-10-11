using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>What one pass of the Health Wiki's daily upkeep did.</summary>
    [ExcludeFromCodeCoverage]
    public class HealthWikiMaintenanceResult
    {
        // N6: doctors reminded, and doctors who were due but WhatsApp did not take it (or had no number).
        public int RemindersSent { get; set; }
        public int RemindersNotSent { get; set; }
        // "Needs detail" requests with no reply for 30 days, now closed.
        public int TopicsClosed { get; set; }
        public int OtpsRemoved { get; set; }
        public int SessionsRemoved { get; set; }
    }
}
