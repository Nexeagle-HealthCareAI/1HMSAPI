using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRoomsResponseModel
    {
        public List<RoomItemResponseModel> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RoomItemResponseModel
    {
        public Guid RoomId { get; set; }
        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public string? FloorNo { get; set; }
        public string? RoomNo { get; set; }
        public string? RoomType { get; set; }
        public int CapacityInRoom { get; set; }
        public decimal DailyRate { get; set; }
        public bool IsActive { get; set; }
        public int BedCount { get; set; }
        public int OccupiedBedCount { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RoomDetailResponseModel : RoomItemResponseModel
    {
        public Guid HospitalId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public List<BedMasterItemResponseModel> Beds { get; set; } = new();
    }
}
