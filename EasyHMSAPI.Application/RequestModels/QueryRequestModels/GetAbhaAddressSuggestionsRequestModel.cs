using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAbhaAddressSuggestionsRequestModel : IRequest<AbdmAddressSuggestionsResponseModel>
    {
        public string TxnId { get; set; } = string.Empty;
        /// <summary>Optional: only so the call shows in this hospital's activity log.</summary>
        public Guid? HospitalId { get; set; }
    }
}
