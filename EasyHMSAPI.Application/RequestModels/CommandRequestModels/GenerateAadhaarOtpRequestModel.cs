using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GenerateAadhaarOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string AadhaarNumber { get; set; } = string.Empty;
        /// <summary>Id returned by abdm/consent. The OTP is refused without a valid, unexpired consent recorded by this user for this hospital.</summary>
        public Guid? ConsentId { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
