using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsultTimelineResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }

        public int PrescriptionValidDays { get; set; }
        public bool NeverExpires { get; set; }

        // Anchor: most recent fee visit (New / Old-Fee) for this patient + doctor.
        public ConsultTimelineVisit? LastFeeVisit { get; set; }
        public DateTime? LastPaidDate { get; set; }
        public DateTime? ValidUptoDate { get; set; }

        // Free (Old/No-Fee) follow-ups since the anchor.
        public int FreeFollowUpCount { get; set; }

        // Preview of what THIS booking will be (drives the popup's fee section).
        public ConsultNextVisit NextVisit { get; set; } = new();

        // Per-visit history (most recent first).
        public List<ConsultTimelineVisit> History { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ConsultTimelineVisit
    {
        public Guid AppointmentId { get; set; }
        public DateTime ApptDate { get; set; }
        public string? AppointmentType { get; set; }
        public string? StatusCode { get; set; }
        public bool ConsultCharged { get; set; }
        public bool ConsultPaid { get; set; }
        public decimal Amount { get; set; }
        public string? ReceiptNo { get; set; }
        public Guid? EncounterId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConsultNextVisit
    {
        public string AppointmentType { get; set; } = "New";
        public bool FeeApplies { get; set; }
        public decimal Fee { get; set; }
    }
}
