using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteChargeMasterResponseModel
    {
        public bool IsSucess { get; set; }
        public string? Message { get; set; }
    }
}
