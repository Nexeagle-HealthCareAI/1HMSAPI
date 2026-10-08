using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HospitalUpdateResponseModel
    {
        /// <summary>True when the caller is not authorised for this action (controller maps to 403).</summary>
        public bool Forbidden { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? HospitalId { get; set; }
    }
} 