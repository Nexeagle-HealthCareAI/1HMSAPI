using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionDayBillsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public AdmissionDayBillsData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionDayBillsData
    {
        public Guid AdmissionId { get; set; }
        public Guid EncounterId { get; set; }
        public string? PatientId { get; set; }
        public DateTime AdmittedAt { get; set; }

        public int TotalDays { get; set; }
        public decimal TotalCharged { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal Balance { get; set; }

        public List<AdmissionDayView> Days { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionDayView
    {
        public int DayNumber { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public bool IsClosed { get; set; }
        public bool IsCurrent { get; set; }
        public Guid? AdmissionDayBillId { get; set; }
        public string? InterimBillNo { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CumulativeNetAmount { get; set; }
        public List<AdmissionDayLineView> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionDayLineView
    {
        public Guid ChargeEventId { get; set; }
        public string? CategoryCode { get; set; }
        public string? DisplayName { get; set; }
        public DateTime ServiceDate { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetAmount { get; set; }
    }
}
