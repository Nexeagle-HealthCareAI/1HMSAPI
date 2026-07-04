using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetCreditApprovalsRequestModel : IRequest<GetCreditApprovalsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }
        public int? Take { get; set; }
    }
}
