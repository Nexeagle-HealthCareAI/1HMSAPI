using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class SearchPatientRequestModel : IRequest<SearchPatientResponseModel>
    {
        public string? By { get; set; }
        public string? Q { get; set; }
        public string Scope { get; set; } = "local";
    }
}
