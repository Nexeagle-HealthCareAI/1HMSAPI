using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RequestSurgeryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? SurgeryCaseId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateSurgeryCaseStatusResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
