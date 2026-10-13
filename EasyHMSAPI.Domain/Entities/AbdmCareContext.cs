using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One OPD visit or IPD admission offered to ABDM as a care context, with how far its
    /// link to the patient's ABHA has got.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmCareContext")]
    public class AbdmCareContext
    {
        [Key]
        public Guid CareContextId { get; set; }
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public Guid SourceId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string HiTypes { get; set; } = string.Empty;
        public string? AbhaNumber { get; set; }
        public string? AbhaAddress { get; set; }
        public string LinkStatus { get; set; } = AbdmLinkStatus.NotLinked;
        public Guid? TokenExchangeId { get; set; }
        public Guid? LinkExchangeId { get; set; }
        public string? LastErrorCode { get; set; }
        public string? LastErrorMessage { get; set; }
        public DateTime? LinkedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public static class AbdmLinkStatus
    {
        public const string NotLinked = "NOT_LINKED";
        /// <summary>Asked ABDM for a link token; waiting for on-generate-token.</summary>
        public const string TokenRequested = "TOKEN_REQUESTED";
        /// <summary>Sent link/carecontext; waiting for on_carecontext.</summary>
        public const string LinkRequested = "LINK_REQUESTED";
        public const string Linked = "LINKED";
        public const string Failed = "FAILED";
    }

    public static class AbdmCareContextSource
    {
        public const string Opd = "OPD";
        public const string Ipd = "IPD";
    }

    /// <summary>A link token ABDM issued for one ABHA address at one HIP.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmLinkToken")]
    public class AbdmLinkToken
    {
        [Key]
        public Guid LinkTokenId { get; set; }
        public Guid HospitalId { get; set; }
        public string HipId { get; set; } = string.Empty;
        public string AbhaAddress { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public Guid? SourceExchangeId { get; set; }
        public DateTime ReceivedAt { get; set; }
    }
}
