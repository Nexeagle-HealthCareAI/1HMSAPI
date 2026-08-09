using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAbhaAddressSuggestionsRequestModel : IRequest<AbdmAddressSuggestionsResponseModel>
    {
        public string TxnId { get; set; } = string.Empty;
    }
}
