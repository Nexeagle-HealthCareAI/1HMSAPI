using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class LogInfectionEventResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? InfectionEventId { get; set; }
    }
}
