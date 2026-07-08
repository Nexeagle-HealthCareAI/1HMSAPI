using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Inventory store hierarchy node (central/ward/OT/pharmacy/CSSD/blood-bank/etc.), self-referencing
    /// via ParentStoreId. Every hospital gets one MAIN store auto-provisioned; everything else nests
    /// under it or another store.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Store
    {
        [Key]
        public Guid StoreId { get; set; }
        public Guid HospitalId { get; set; }

        public string StoreCode { get; set; } = null!;
        public string StoreName { get; set; } = null!;
        public string StoreType { get; set; } = null!;   // MAIN/SUB/DEPARTMENT/OT/PHARMACY/COLD_CHAIN/NARCOTIC/BLOOD_BANK/CSSD

        // Optional linking to a specific clinical board context (e.g. OT, ICU, WARD)
        public string? AssignedBoard { get; set; }

        public Guid? ParentStoreId { get; set; }

        public decimal? MinTempCelsius { get; set; }
        public decimal? MaxTempCelsius { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
