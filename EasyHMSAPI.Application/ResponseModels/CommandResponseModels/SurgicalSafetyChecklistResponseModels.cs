using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordSignInResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordTimeOutResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordSignOutResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
