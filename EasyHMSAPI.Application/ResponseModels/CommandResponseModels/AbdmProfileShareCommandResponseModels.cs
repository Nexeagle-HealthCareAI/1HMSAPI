using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordAbdmProfileShareResponseModel
    {
        // The share was accepted: newly stored, or an ABDM retry of one already stored.
        public bool Accepted { get; set; }
        public bool Duplicate { get; set; }
        public string? Message { get; set; }
        // What the on-share acknowledgement needs.
        public string? CallbackRequestId { get; set; }
        public string? AbhaAddress { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HandleAbdmProfileShareResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaveAbdmFacilityResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
