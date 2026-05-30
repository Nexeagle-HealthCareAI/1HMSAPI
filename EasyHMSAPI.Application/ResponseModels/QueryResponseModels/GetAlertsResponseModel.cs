using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAlertsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AlertItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AlertItem
    {
        public Guid AlertId { get; set; }
        public string AlertCode { get; set; } = null!;
        public string Severity { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Body { get; set; }

        public string? PatientId { get; set; }
        public Guid? AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }

        public string? AudienceRoles { get; set; }
        public Guid? AudienceUserId { get; set; }
        public string? AudienceWardCode { get; set; }

        public string Status { get; set; } = null!;

        public DateTime RaisedAt { get; set; }
        public string? RaisedBy { get; set; }
        public string? SourceModule { get; set; }

        public bool DispatchSms { get; set; }
        public bool DispatchWhatsApp { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public string? DispatchError { get; set; }

        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
        public string? AcknowledgeNote { get; set; }

        public DateTime? DismissedAt { get; set; }
        public string? DismissedBy { get; set; }
        public string? DismissReason { get; set; }

        public DateTime? SnoozedUntil { get; set; }
        public string? PayloadJson { get; set; }
    }
}
