using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GenerateAbdmMobileOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
    }
}
