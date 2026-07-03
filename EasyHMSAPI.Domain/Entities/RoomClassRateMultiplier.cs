using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Hospital-level room-class rate multiplier (100 = no change). Applied on top of any payer-rate override.</summary>
    [ExcludeFromCodeCoverage]
    [Table("RoomClassRateMultiplier")]
    public class RoomClassRateMultiplier
    {
        [Key]
        public Guid RoomClassRateMultiplierId { get; set; }
        public Guid HospitalId { get; set; }
        public string RoomType { get; set; } = null!;
        public decimal MultiplierPercent { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
