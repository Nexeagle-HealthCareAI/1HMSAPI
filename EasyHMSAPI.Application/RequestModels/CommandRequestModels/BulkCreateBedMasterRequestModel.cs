using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class BulkCreateBedMasterRequestModel : IRequest<BulkCreateBedMasterResponseModel>
    {
        public Guid HospitalId { get; set; }
        // When set, Ward/Room/rate/capacity fields below are ignored and instead synced from the
        // Room master row, and the room's remaining capacity caps how many beds can be created.
        public Guid? RoomId { get; set; }
        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public string? FloorNo { get; set; }
        public string? RoomCode { get; set; }
        public string? RoomType { get; set; }
        public int? CapacityInRoom { get; set; }
        public decimal WardRoomDailyRate { get; set; }
        public decimal? BedDailyRateOverride { get; set; }
        public decimal? IncentiveAmount { get; set; }
        public string? GenderRestriction { get; set; }
        public string? StatusCode { get; set; }
        public bool IsActive { get; set; } = true;

        // Prefix used to build sequential bed codes, e.g. "ICU" → ICU-01, ICU-02… The server
        // determines the starting number from existing beds for this prefix.
        public string? BedCodePrefix { get; set; }
        public int Count { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
