using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class NumberSeries
    {
        [Key]
        public Guid SeriesId { get; set; }
        public Guid HospitalId { get; set; }
        [MaxLength(50)]
        public string? SeriesCode { get; set; }
        [MaxLength(50)]
        public string? Prefix { get; set; }
        [MaxLength(20)]
        public string? YearFormat { get; set; }
        [MaxLength(5)]
        public string? Separator { get; set; }
        public long CurrentValue { get; set; }
        public int PadLength { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
