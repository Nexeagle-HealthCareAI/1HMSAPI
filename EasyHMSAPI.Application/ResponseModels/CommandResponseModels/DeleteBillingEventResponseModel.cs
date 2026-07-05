using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteBillingEventResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public bool PendingApproval { get; set; }
        public Guid? CreditApprovalId { get; set; }
    }
}
