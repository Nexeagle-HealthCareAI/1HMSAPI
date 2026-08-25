using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetLapsedPatientsRequestModel : IRequest<GetLapsedPatientsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
    }
}
