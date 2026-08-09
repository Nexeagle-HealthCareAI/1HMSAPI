using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateAbhaAddressRequestModel : IRequest<AbdmEnrollResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public string AbhaAddress { get; set; } = string.Empty;
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
