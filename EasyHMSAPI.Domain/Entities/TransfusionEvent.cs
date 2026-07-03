using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("TransfusionEvent")]
    public class TransfusionEvent
    {
        [Key]
        public Guid TransfusionEventId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid BloodBagId { get; set; }

        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public decimal VolumeGivenMl { get; set; }

        public string? VitalsBefore { get; set; }
        public string? VitalsAfter { get; set; }

        public string Reaction { get; set; } = null!;   // NONE/MILD/SEVERE/ANAPHYLAXIS
        public string? ReactionNotes { get; set; }

        public string AdministeredBy { get; set; } = null!;
        public Guid? AdministeredByUserId { get; set; }
        public string WitnessName { get; set; } = null!;
        public Guid? WitnessUserId { get; set; }

        public string? Notes { get; set; }
        public Guid? ChargeEventId { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
