using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Full doctor-assignment history for one admission -- each row is one doctor's tenure
    // span (AssignedAt -> UnassignedAt, or "current" while ACTIVE).
    [ExcludeFromCodeCoverage]
    public class GetAdmissionDoctorHistoryRequestModel : IRequest<GetAdmissionDoctorHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
