using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Encounter
    {
        public Guid EncounterId { get; set; }
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public string? EncounterTypeCode { get; set; }
        public string? SourceType { get; set; }
        public Guid? SourceId { get; set; }
        public Guid? PrimaryDoctorId { get; set; }
        public string? StatusCode { get; set; }
        public Guid? ReferredByReferrerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
