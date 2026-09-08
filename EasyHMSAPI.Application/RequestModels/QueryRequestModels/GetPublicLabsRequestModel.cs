using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Platform-wide pathology-lab directory for Doctor Dekho. LabId reuses this same paginated
    // query with PageSize=1 for the single-lab detail fetch, the same trick GetPublicDoctorsHandler
    // uses for GetDoctorById -- no separate detail handler.
    [ExcludeFromCodeCoverage]
    public class GetPublicLabsRequestModel : IRequest<GetPublicLabsResponseModel>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Search { get; set; }
        public Guid? LabId { get; set; }
    }
}
