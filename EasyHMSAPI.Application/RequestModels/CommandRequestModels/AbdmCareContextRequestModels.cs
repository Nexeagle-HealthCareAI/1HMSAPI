using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Starts linking one OPD visit (SourceType "OPD", SourceId = ApptId) or IPD admission
    /// (SourceType "IPD", SourceId = AdmissionId) to the patient's ABHA. Returns at once.</summary>
    [ExcludeFromCodeCoverage]
    public class LinkAbdmCareContextRequestModel : IRequest<AbdmCareContextResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public Guid SourceId { get; set; }
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAbdmCareContextsRequestModel : IRequest<AbdmCareContextListResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
    }
}
