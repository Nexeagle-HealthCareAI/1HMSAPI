using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmReadinessIssue
    {
        /// <summary>"blocker": calls are refused before sending. "warning": calls go out but their
        /// answers cannot reach us.</summary>
        public string Level { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    public class AbdmM2ReadinessResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? HipId { get; set; }
        public bool BridgeSettingsPresent { get; set; }
        /// <summary>When ABDM last reached our bridge with an accepted callback for this hospital.</summary>
        public DateTime? LastCallbackAt { get; set; }
        public List<AbdmReadinessIssue> Issues { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AbdmExchangeDto
    {
        public string RequestId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? SubjectRef { get; set; }
        /// <summary>SENT | ACCEPTED | DONE | REFUSED | ANSWERED | NO_ANSWER (accepted, nothing back in the window).</summary>
        public string Status { get; set; } = string.Empty;
        public int? HttpStatus { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
        /// <summary>Milliseconds from sending to the callback, when one came.</summary>
        public long? WaitMs { get; set; }
        public string? CallbackPath { get; set; }
        public string? CallbackHeaders { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AbdmExchangeListResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int CallbackWindowSeconds { get; set; }
        public List<AbdmExchangeDto> Exchanges { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AbdmInboundDto
    {
        public string Path { get; set; } = string.Empty;
        public string Verdict { get; set; } = string.Empty;
        public string? FacilityId { get; set; }
        public string HeaderNames { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessError { get; set; }
        /// <summary>ABDM asked and this system has not replied yet.</summary>
        public bool Unanswered { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AbdmInboundListResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AbdmInboundDto> Requests { get; set; } = new();
    }
}
