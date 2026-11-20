using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class VerifyAbdmMobileOtpRequestModel : IRequest<AbdmEnrollResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
