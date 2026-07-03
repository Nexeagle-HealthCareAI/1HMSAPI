using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert by (HospitalId, ChargeId, PayerType) — one override row per charge/payer combination.
    [ExcludeFromCodeCoverage]
    public class UpsertChargeMasterPayerRateRequestModel : IRequest<UpsertChargeMasterPayerRateResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid ChargeId { get; set; }
        public string PayerType { get; set; } = null!;
        public decimal OverrideRate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // Upsert by (HospitalId, RoomType) — one multiplier row per room type.
    [ExcludeFromCodeCoverage]
    public class UpsertRoomClassRateMultiplierRequestModel : IRequest<UpsertRoomClassRateMultiplierResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public string RoomType { get; set; } = null!;
        public decimal MultiplierPercent { get; set; }
    }
}
