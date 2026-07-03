using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Set/tray-level CSSD asset — CurrentStatus/CurrentLocation are denormalized from InstrumentSetMovement.</summary>
    [ExcludeFromCodeCoverage]
    [Table("InstrumentSet")]
    public class InstrumentSet
    {
        [Key]
        public Guid InstrumentSetId { get; set; }
        public Guid HospitalId { get; set; }

        public string SetCode { get; set; } = null!;
        public string SetName { get; set; } = null!;
        public string? Category { get; set; }
        public string? ItemComposition { get; set; }

        public string CurrentStatus { get; set; } = null!;
        public string? CurrentLocation { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
