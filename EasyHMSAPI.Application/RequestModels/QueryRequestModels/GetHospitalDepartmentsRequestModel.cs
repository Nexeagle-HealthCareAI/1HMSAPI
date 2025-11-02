using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalDepartmentsRequestModel : IRequest<GetHospitalDepartmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
