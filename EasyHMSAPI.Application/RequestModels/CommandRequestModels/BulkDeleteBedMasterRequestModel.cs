using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class BulkDeleteBedMasterRequestModel : IRequest<BulkDeleteBedMasterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public List<Guid> BedIds { get; set; } = new();

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
