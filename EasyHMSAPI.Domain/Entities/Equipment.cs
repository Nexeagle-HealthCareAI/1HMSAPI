using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Biomedical/ICT/facility asset register — wires the previously-unused create_tables_equipment.sql
    /// schema (finalized ahead of application code; do not add/rename columns here without a matching
    /// guarded migration).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Equipment
    {
        [Key]
        public Guid EquipmentId { get; set; }
        public Guid HospitalId { get; set; }

        public string AssetCode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? Manufacturer { get; set; }

        public string Category { get; set; } = null!;   // BIOMEDICAL/ICT/FACILITY/FURNITURE/OTHER

        public string? Location { get; set; }
        public string? Department { get; set; }
        public string? AmcVendor { get; set; }

        public DateTime? InstalledAt { get; set; }
        public DateTime? WarrantyEndAt { get; set; }
        public DateTime? AmcEndAt { get; set; }

        public int? PmIntervalDays { get; set; }
        public DateTime? LastServiceAt { get; set; }
        public DateTime? NextDueAt { get; set; }

        public string Status { get; set; } = null!;   // ACTIVE/UNDER_MAINTENANCE/RETIRED

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
