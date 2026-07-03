using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBloodBagPoolRequestModel : IRequest<GetBloodBagPoolResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Component { get; set; }
        public string? BloodGroup { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAdmissionTransfusionHistoryRequestModel : IRequest<GetAdmissionTransfusionHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
