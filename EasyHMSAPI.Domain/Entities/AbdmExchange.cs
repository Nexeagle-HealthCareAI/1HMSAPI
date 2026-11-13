using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One outbound ABDM HIE-CM call (M2/M3). Most answer twice — a 202 now and a callback
    /// later — and <see cref="RequestId"/> joins the two.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmExchange")]
    public class AbdmExchange
    {
        [Key]
        public Guid ExchangeId { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public Guid? HospitalId { get; set; }
        public string? FacilityId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? SubjectRef { get; set; }
        public bool ExpectsCallback { get; set; }
        public string Status { get; set; } = AbdmExchangeStatus.Sent;
        public int? HttpStatus { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public Guid? AnsweredByInboundId { get; set; }
    }

    public static class AbdmExchangeStatus
    {
        public const string Sent = "SENT";
        /// <summary>202 received; the outcome is still to come on a callback.</summary>
        public const string Accepted = "ACCEPTED";
        /// <summary>2xx received and no callback is expected (our replies to ABDM's own requests).</summary>
        public const string Done = "DONE";
        /// <summary>Refused synchronously, or locally before sending: nothing was ever pending.</summary>
        public const string Refused = "REFUSED";
        public const string Answered = "ANSWERED";
    }

    /// <summary>One delivery ABDM made to our bridge URL, recorded before anything parses it.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmInbound")]
    public class AbdmInbound
    {
        [Key]
        public Guid InboundId { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? HeaderRequestId { get; set; }
        public string? AnswersRequestId { get; set; }
        public string? FacilityId { get; set; }
        public Guid? HospitalId { get; set; }
        public string HeaderNames { get; set; } = string.Empty;
        public string? RawBody { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string? ProcessError { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public static class AbdmInboundVerdict
    {
        public const string Accepted = "ACCEPTED";
        public const string Duplicate = "DUPLICATE";
        public const string NoBearer = "REJECTED_NO_BEARER";
        public const string UnknownFacility = "REJECTED_UNKNOWN_FACILITY";
        public const string UnknownRequest = "REJECTED_UNKNOWN_REQUEST";
    }
}
