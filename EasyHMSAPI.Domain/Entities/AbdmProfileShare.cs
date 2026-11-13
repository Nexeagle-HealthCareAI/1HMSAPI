using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A profile a patient shared by scanning the facility's counter QR in their ABHA/PHR
    /// app (ABDM M1 "Scan Health Facility QR"). One row per inbound /v3/hip/patient/profile/share
    /// callback.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmProfileShare")]
    public class AbdmProfileShare
    {
        [Key]
        public Guid ProfileShareId { get; set; }
        public Guid HospitalId { get; set; }
        public string HipId { get; set; } = string.Empty;
        public string? CounterId { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string? AbhaNumber { get; set; }
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Address { get; set; }
        public string? LinkToken { get; set; }
        public string StatusCode { get; set; } = "NEW";
        public DateTime? HandledAt { get; set; }
        public string? HandledBy { get; set; }
        public string? RawPayload { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    /// <summary>Maps a hospital to its Health Facility Registry (HFR) / HIP ID — the only facility
    /// identifier ABDM's profile-share callback carries.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmFacility")]
    public class AbdmFacility
    {
        [Key]
        public Guid HospitalId { get; set; }
        public string HipId { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
