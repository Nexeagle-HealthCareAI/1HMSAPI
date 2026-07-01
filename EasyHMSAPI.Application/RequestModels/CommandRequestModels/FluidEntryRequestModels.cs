using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records one intake/output entry. Pure insert, no transaction.
    [ExcludeFromCodeCoverage]
    public class RecordFluidEntryRequestModel : IRequest<RecordFluidEntryResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string Direction { get; set; } = null!;   // IN / OUT
        public string Subtype { get; set; } = null!;
        public decimal VolumeMl { get; set; }
        public string? Description { get; set; }
        public string? RouteOrSite { get; set; }
        public string? Colour { get; set; }
        public string? Notes { get; set; }
    }
}
