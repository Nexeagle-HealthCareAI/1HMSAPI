using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertConsultantFeeConfigResponseModel
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
    }
}
