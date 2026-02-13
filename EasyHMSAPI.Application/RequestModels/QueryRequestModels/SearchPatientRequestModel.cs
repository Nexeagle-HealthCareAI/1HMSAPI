using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class SearchPatientRequestModel : IRequest<SearchPatientResponseModel>
    {
        public string? SearchText { get; set; }
        public Guid HospitalId { get; set; }
    }
}
