using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertChargeMasterResponseModel
    {
        public Guid ChargeId { get; set; }
        public string? ChargeCode { get; set; }
        public string? RowVersion { get; set; }
    }
}
