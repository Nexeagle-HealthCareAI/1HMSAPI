using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertChargeMasterPayerRateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ChargeMasterPayerRateId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpsertRoomClassRateMultiplierResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? RoomClassRateMultiplierId { get; set; }
    }
}
