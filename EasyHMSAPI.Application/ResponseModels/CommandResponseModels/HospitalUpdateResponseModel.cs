using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HospitalUpdateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? HospitalId { get; set; }
    }
} 