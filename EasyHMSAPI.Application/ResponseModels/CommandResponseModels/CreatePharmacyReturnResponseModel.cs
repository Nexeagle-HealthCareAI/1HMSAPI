using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreatePharmacyReturnResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid ReturnId { get; set; }
        public string? ReturnNo { get; set; }
        public decimal TotalRefundAmount { get; set; }
    }
}
