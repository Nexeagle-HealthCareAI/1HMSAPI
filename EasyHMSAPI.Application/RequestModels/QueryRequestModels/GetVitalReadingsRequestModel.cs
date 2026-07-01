using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Vitals trend for an admission over a window (defaults to the last 7 days when omitted).
    [ExcludeFromCodeCoverage]
    public class GetVitalReadingsRequestModel : IRequest<GetVitalReadingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
    }
}
