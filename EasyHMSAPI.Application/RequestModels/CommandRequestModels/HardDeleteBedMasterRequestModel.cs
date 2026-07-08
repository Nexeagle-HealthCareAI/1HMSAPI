using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class HardDeleteBedMasterRequestModel : IRequest<HardDeleteBedMasterResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid BedId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BulkHardDeleteBedMasterRequestModel : IRequest<BulkHardDeleteBedMasterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public List<Guid> BedIds { get; set; } = new();
    }
}
