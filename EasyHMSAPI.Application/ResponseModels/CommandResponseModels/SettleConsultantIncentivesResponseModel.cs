using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SettleConsultantIncentivesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int SettledCount { get; set; }
        public decimal SettledTotal { get; set; }
    }
}
