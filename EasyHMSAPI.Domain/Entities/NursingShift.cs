using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A hospital's nursing shift definition (table: easyHMSDatabase create_tables_nursing_shift.sql).</summary>
    [ExcludeFromCodeCoverage]
    [Table("NursingShift")]
    public class NursingShift
    {
        [Key]
        public Guid NursingShiftId { get; set; } = Guid.NewGuid();
        public Guid HospitalId { get; set; }

        [MaxLength(30)]
        public string ShiftCode { get; set; } = null!;

        [MaxLength(60)]
        public string Label { get; set; } = null!;

        [MaxLength(5)]
        public string? StartTime { get; set; }

        [MaxLength(5)]
        public string? EndTime { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedByUserId { get; set; }
    }
}
