using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DischargeMedication
    {
        public Guid DischargeMedicationId { get; set; }
        public Guid DischargeSummaryId { get; set; }
        public string? MedicineName { get; set; }
        public string? Dosage { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public string? Durations { get; set; }
        public string? Instructions { get; set; }
        public string? SaltName { get; set; }
        public int? DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
