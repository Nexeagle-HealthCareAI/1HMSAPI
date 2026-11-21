using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class LinkAbdmFacilityServicesRequestModel : IRequest<LinkAbdmFacilityServicesResponseModel>
    {
        public Guid HospitalId { get; set; }
        /// <summary>The name patients see in their app. Empty: made from the hospital's name.</summary>
        public string? HipName { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
