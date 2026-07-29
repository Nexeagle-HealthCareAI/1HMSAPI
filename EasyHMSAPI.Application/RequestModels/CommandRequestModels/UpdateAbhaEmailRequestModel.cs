using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateAbhaEmailRequestModel : IRequest<AbdmUpdateResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string SessionTxnId { get; set; } = string.Empty;
        public string NewEmail { get; set; } = string.Empty;
    }
}
