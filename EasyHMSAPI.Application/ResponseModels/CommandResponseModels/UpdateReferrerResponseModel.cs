using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateReferrerResponseModel
    {
        public bool Success { get; set; }
        public Guid ReferrerId { get; set; }
        public string? Message { get; set; }
    }
}
