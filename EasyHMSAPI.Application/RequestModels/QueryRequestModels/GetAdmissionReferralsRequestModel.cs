using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferralsRequestModel : IRequest<GetAdmissionReferralsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? StatusCode { get; set; }      // PENDING / CONVERTED / NOT_ADMITTED / FOLLOW_UP
        public string? CaseType { get; set; }         // EMERGENCY / PLANNED / URGENT
        public Guid? ReferringDoctorId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
