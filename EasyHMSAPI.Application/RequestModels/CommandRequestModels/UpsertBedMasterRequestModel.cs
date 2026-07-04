using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertBedMasterRequestModel : IRequest<UpsertBedMasterResponseModel>
    {
        public Guid? BedId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        // When set, Ward/Room/rate/capacity fields below are ignored on CREATE and instead synced
        // from the Room master row, so a bed always matches the room it was added to.
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
        public string? BedCode { get; set; }
        public string? BedName { get; set; }
        public string? StatusCode { get; set; }
        public string? GenderRestriction { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
