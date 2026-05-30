using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAlertsRequestModel : IRequest<GetAlertsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
        public string? Severity { get; set; }
        public string? AlertCode { get; set; }
        public Guid? AdmissionId { get; set; }
        public Guid? AudienceUserId { get; set; }
        public string? Role { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public int? Take { get; set; }
    }
}
