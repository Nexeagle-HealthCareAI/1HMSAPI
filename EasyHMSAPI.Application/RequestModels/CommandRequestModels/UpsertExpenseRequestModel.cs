using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertExpenseRequestModel : IRequest<UpsertExpenseResponseModel>
    {
        public Guid? ExpenseId { get; set; }
        public Guid HospitalId { get; set; }
        public DateTime? ExpenseDate { get; set; }
        public string? CategoryCode { get; set; }
        public string? Vendor { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMode { get; set; }
        public string? StatusCode { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Notes { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
