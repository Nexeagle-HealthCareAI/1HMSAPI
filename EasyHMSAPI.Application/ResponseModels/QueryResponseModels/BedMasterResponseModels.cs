using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBedMastersResponseModel
    {
        public List<BedMasterItemResponseModel> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BedMasterItemResponseModel
    {
        public Guid BedId { get; set; }
        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public string? FloorNo { get; set; }
        public string? RoomCode { get; set; }
        public string? RoomType { get; set; }
        public int CapacityInRoom { get; set; }
        public decimal WardRoomDailyRate { get; set; }
        public decimal? BedDailyRateOverride { get; set; }
        public decimal EffectiveDailyRate { get; set; }
        public decimal? IncentiveAmount { get; set; }
        public string? BedCode { get; set; }
        public string? BedName { get; set; }
        public string? StatusCode { get; set; }
        public string? GenderRestriction { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime? LastStatusAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public string? RowVersion { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BedMasterDetailResponseModel : BedMasterItemResponseModel
    {
        public Guid HospitalId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
