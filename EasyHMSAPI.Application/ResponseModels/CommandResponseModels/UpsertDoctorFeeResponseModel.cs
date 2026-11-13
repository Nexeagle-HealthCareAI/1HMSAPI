using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertDoctorFeeResponseModel
    {
        /// <summary>True when the caller is not authorised for this action (controller maps to 403).</summary>
        public bool Forbidden { get; set; }
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
    }
}
