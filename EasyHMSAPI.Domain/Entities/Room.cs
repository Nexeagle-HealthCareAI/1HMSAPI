using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Room
    {
        [Key]
        public Guid RoomId { get; set; }
        public Guid HospitalId { get; set; }

        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public string? FloorNo { get; set; }

        public string? RoomNo { get; set; }
        public string? RoomType { get; set; }
        public int CapacityInRoom { get; set; }
        public decimal DailyRate { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
