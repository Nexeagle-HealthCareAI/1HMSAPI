using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class BedMaster
    {
        [Key]
        public Guid BedId { get; set; }
        public Guid HospitalId { get; set; }
        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public string? FloorNo { get; set; }
        public string? RoomCode { get; set; }
        public string? RoomType { get; set; }
        public int? CapacityInRoom { get; set; }
        // Links to the Room master row this bed belongs to, when created that way. Null for beds
        // created the old way (free-text RoomCode only, no Room master row).
        public Guid? RoomId { get; set; }
        public decimal WardRoomDailyRate { get; set; }
        public decimal? BedDailyRateOverride { get; set; }
        public decimal? IncentiveAmount { get; set; }
        public string? BedCode { get; set; }
        public string? BedName { get; set; }
        public string? StatusCode { get; set; }
        public string? GenderRestriction { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime? LastStatusAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
