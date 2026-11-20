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
        /// <summary>True for "Resend OTP": ABDM is asked again under the transaction this consent already opened, so the
        /// OTP is resent, not a new request started. The transaction id is the one we stored, never one the browser sends.</summary>
        public bool Resend { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
