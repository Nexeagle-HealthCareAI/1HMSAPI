using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Full "Referred by" assignment history for one admission -- each row is one referrer's
    // tenure span (AssignedAt -> UnassignedAt, or "current" while ACTIVE).
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferrerHistoryRequestModel : IRequest<GetAdmissionReferrerHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
