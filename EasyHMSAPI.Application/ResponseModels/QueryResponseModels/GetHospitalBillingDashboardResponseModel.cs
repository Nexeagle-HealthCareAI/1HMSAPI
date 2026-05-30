using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalBillingDashboardResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public List<HospitalBillingDashboardData>? Data { get; set; }
    }

    public class HospitalBillingDashboardData
    {
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public List<DashboardEncounterDetail>? Encounters { get; set; }
    }

    public class DashboardEncounterDetail
    {
        public Guid EncounterId { get; set; }
        public string? VisitType { get; set; }
        public string? InvoiceNo { get; set; }
        public Guid InvoiceId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? DoctorName { get; set; }
        public decimal NetAmount { get; set; }
        public decimal DueAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? Status { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public bool IsCancelled { get; set; }
        public string? CancelReason { get; set; }
    }
}
