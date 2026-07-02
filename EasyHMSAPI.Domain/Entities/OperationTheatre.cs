using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("OperationTheatre")]
    public class OperationTheatre
    {
        [Key]
        public Guid TheatreId { get; set; }
        public Guid HospitalId { get; set; }
        public string TheatreCode { get; set; } = null!;
        public string TheatreName { get; set; } = null!;
        public string Status { get; set; } = null!;   // AVAILABLE/IN_USE/CLEANING/UNAVAILABLE
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
