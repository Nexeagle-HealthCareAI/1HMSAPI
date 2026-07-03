using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRateCardConfigResponseModel
    {
        public List<ChargeMasterPayerRateDataModel>? PayerRates { get; set; }
        public List<RoomClassRateMultiplierDataModel>? RoomMultipliers { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ChargeMasterPayerRateDataModel
    {
        public Guid ChargeMasterPayerRateId { get; set; }
        public Guid ChargeId { get; set; }
        public string? ChargeDisplayName { get; set; }
        public string? ChargeCode { get; set; }
        public string? PayerType { get; set; }
        public decimal OverrideRate { get; set; }
        public bool IsActive { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RoomClassRateMultiplierDataModel
    {
        public Guid RoomClassRateMultiplierId { get; set; }
        public string? RoomType { get; set; }
        public decimal MultiplierPercent { get; set; }
    }
}
