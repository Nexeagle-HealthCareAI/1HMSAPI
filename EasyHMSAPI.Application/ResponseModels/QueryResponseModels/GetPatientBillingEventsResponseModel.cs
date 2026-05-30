using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientBillingEventsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public GetPatientBillingEventsData? Data { get; set; }
    }

    public class GetPatientBillingEventsData
    {
        public string? PatientId { get; set; }
        public List<PatientEncounterInvoiceDetail>? Encounters { get; set; }
    }

    public class PatientEncounterInvoiceDetail
    {
        public Guid EncounterId { get; set; }
        public string? InvoiceNo { get; set; }
        public Guid InvoiceId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? DoctorName { get; set; }
        public string? Status { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public bool IsCancelled { get; set; }
        public string? CancelReason { get; set; }
    }
}
