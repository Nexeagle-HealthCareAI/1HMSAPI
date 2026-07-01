using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateNursingCarePlanItemResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? CarePlanItemId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ResolveNursingCarePlanItemResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? CarePlanItemId { get; set; }
        public string? StatusCode { get; set; }
    }
}
