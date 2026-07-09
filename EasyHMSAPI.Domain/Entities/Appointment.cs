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
    }
}
