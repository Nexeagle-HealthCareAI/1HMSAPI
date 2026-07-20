using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("Appointments")]
    public class Appointment
    {
        [Key]
        public Guid ApptId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? PatientId { get; set; }
        public DateTime ApptDate { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string? Reason { get; set; }
        public string? InsuranceId { get; set; }
        public string? PaymentMode { get; set; }
        public string? CurrentStatusCode { get; set; }
        public string? StatusHistoryJson { get; set; }
        public DateTime LastStatusCodeAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? AppointmentType { get; set; }
        public string? PdfUrl { get; set; }
        public DateTime? ValidUptoDate { get; set; }
        public Guid? ReferredByReferrerId { get; set; }
        public string? ReferrerRelation { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? BookingSource { get; set; }

        // Captured only for public (Nexeagle) bookings — abuse-tracking + marketing attribution.
        public string? BookingIpAddress { get; set; }
        public string? BookingReferrerUrl { get; set; }
        public string? BookingUtmCampaign { get; set; }

        // The OTP-verified patient-session mobile active at the moment this booking was made (via
        // PublicController.BookAppointment), NULL when the visitor was a guest. Distinct from this
        // appointment's own contact mobile (PatientRegistration.Mobile) — a logged-in visitor can
        // book for a dependent whose mobile differs from their own verified session.
        public string? BookedByMobile { get; set; }
    }
}
