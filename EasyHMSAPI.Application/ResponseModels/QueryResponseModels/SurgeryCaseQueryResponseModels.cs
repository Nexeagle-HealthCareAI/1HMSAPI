using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetSurgeryCasesForAdmissionResponseModel
    {
        public List<SurgeryCaseSummaryDataModel> Cases { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SurgeryCaseSummaryDataModel
    {
        public Guid SurgeryCaseId { get; set; }
        public string ProcedureName { get; set; } = null!;
        public string SurgeryType { get; set; } = null!;
        public string Urgency { get; set; } = null!;
        public string StatusCode { get; set; } = null!;
        public DateTime RequestedAt { get; set; }
        public string? SurgeonName { get; set; }
        public string? AnaesthetistName { get; set; }
        public DateTime? ScheduledStart { get; set; }
        public DateTime? ScheduledEnd { get; set; }
        public string? TheatreName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetSurgeryCaseDetailResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public Guid AdmissionId { get; set; }
        public string ProcedureName { get; set; } = null!;
        public string SurgeryType { get; set; } = null!;
        public string Urgency { get; set; } = null!;
        public string StatusCode { get; set; } = null!;
        public string? SurgeonName { get; set; }
        public string? AnaesthetistName { get; set; }
        public string? CancelledReason { get; set; }

        public OTBookingDetailModel? Booking { get; set; }
        public PreOpAssessmentDetailModel? LatestPreOpAssessment { get; set; }
        public SurgicalSafetyChecklistDetailModel? Checklist { get; set; }
        public IntraOpRecordDetailModel? IntraOpRecord { get; set; }
        public List<IntraOpItemUsageDetailModel> ItemsUsed { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OTBookingDetailModel
    {
        public Guid OTBookingId { get; set; }
        public Guid TheatreId { get; set; }
        public string? TheatreCode { get; set; }
        public string? TheatreName { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public string StatusCode { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class PreOpAssessmentDetailModel
    {
        public Guid PreOpAssessmentId { get; set; }
        public string? AsaGrade { get; set; }
        public bool NpoConfirmed { get; set; }
        public bool AllergiesReviewed { get; set; }
        public bool InvestigationsReviewed { get; set; }
        public bool ConsentConfirmed { get; set; }
        public string? Notes { get; set; }
        public string AssessedBy { get; set; } = null!;
        public DateTime AssessedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SurgicalSafetyChecklistDetailModel
    {
        public DateTime? SignInCompletedAt { get; set; }
        public string? SignInCompletedBy { get; set; }
        public Dictionary<string, bool>? SignInItems { get; set; }
        public string? SignInNotes { get; set; }

        public DateTime? TimeOutCompletedAt { get; set; }
        public string? TimeOutCompletedBy { get; set; }
        public Dictionary<string, bool>? TimeOutItems { get; set; }
        public string? TimeOutNotes { get; set; }

        public DateTime? SignOutCompletedAt { get; set; }
        public string? SignOutCompletedBy { get; set; }
        public Dictionary<string, bool>? SignOutItems { get; set; }
        public string? SignOutNotes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class IntraOpRecordDetailModel
    {
        public Guid IntraOpRecordId { get; set; }
        public string? AnaesthesiaType { get; set; }
        public DateTime? AnaesthesiaStartAt { get; set; }
        public DateTime? AnaesthesiaEndAt { get; set; }
        public DateTime? SurgeryStartAt { get; set; }
        public DateTime? SurgeryEndAt { get; set; }
        public decimal? EstimatedBloodLossMl { get; set; }
        public string? Findings { get; set; }
        public string? ProcedurePerformed { get; set; }
        public string? SurgicalTeam { get; set; }
        public string? ComplicationsNotes { get; set; }
        public string RecordedBy { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class IntraOpItemUsageDetailModel
    {
        public Guid IntraOpItemUsageId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Category { get; set; } = null!;
        public decimal Qty { get; set; }
        public string? LotNumber { get; set; }
        public string? SerialNumber { get; set; }
        public bool IsBilled { get; set; }
        public bool IsStockDeducted { get; set; }
        public string RecordedBy { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }
}
