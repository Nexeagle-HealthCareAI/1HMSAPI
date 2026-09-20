using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Persists one inbound ABDM /v3/hip/patient/profile/share callback (already
    /// authenticated by the callback controller's secret path segment).</summary>
    [ExcludeFromCodeCoverage]
    public class RecordAbdmProfileShareRequestModel : IRequest<RecordAbdmProfileShareResponseModel>
    {
        public string RawBody { get; set; } = string.Empty;
        // The callback's REQUEST-ID header — preferred over any requestId inside the body.
        public string? HeaderRequestId { get; set; }
    }

    /// <summary>Marks a scanned profile as dealt with at the counter, and keeps the ABHA on record.</summary>
    [ExcludeFromCodeCoverage]
    public class HandleAbdmProfileShareRequestModel : IRequest<HandleAbdmProfileShareResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ProfileShareId { get; set; }
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaveAbdmFacilityRequestModel : IRequest<SaveAbdmFacilityResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string HipId { get; set; } = string.Empty;
        public string? LoggedInUserName { get; set; }
    }
}
