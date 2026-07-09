using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AdviseAdmissionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ReferralId { get; set; }
    }
}
