using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateReferrerResponseModel
    {
        public Guid ReferrerId { get; set; }
        public string? ReferrerName { get; set; }
        public string? Message { get; set; }
    }
}
