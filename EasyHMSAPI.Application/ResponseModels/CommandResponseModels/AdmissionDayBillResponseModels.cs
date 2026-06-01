using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CloseAdmissionDayResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionDayBillId { get; set; }
        public int? DayNumber { get; set; }
        public string? InterimBillNo { get; set; }
        public decimal? NetAmount { get; set; }
        public decimal? BalanceDue { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReopenAdmissionDayResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
    }
}
