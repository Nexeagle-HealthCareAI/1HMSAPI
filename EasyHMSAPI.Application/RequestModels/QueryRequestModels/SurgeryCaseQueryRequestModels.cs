using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetSurgeryCasesForAdmissionRequestModel : IRequest<GetSurgeryCasesForAdmissionResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetSurgeryCaseDetailRequestModel : IRequest<GetSurgeryCaseDetailResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }
    }
}
