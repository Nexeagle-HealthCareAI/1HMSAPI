using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RegisterAppointmentResponseModel
    {
        public string? PatientId { get; set; }
        public Guid? AppointmentId { get; set; }
        public string? Status { get; set; }
        public bool IsReminderSent { get; set; }
        public int? TokenNumber { get; set; }
        public string? Message { get; set; }
        public bool BillRefunded { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? RefundReceiptNo { get; set; }
    }
}
