using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPackageTypeRequestModel : IRequest<UpsertPackageTypeResponseModel>
    {
        public Guid? PackageTypeId { get; set; }
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public List<string>? Components { get; set; }
        public bool IsActive { get; set; } = true;
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
