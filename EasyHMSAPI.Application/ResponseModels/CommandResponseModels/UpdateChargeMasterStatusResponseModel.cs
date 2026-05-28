using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateChargeMasterStatusResponseModel
    {
        public bool IsSucess { get; set; }
        public string? Message { get; set; }
    }
}
