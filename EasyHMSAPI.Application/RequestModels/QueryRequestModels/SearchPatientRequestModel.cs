using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class SearchPatientRequestModel : IRequest<SearchPatientResponseModel>
    {
        public string? By { get; set; }
        public string? Q { get; set; }
        public string Scope { get; set; } = "local";
    }
}
