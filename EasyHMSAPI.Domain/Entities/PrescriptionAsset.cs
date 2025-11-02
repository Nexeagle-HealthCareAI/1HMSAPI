using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PrescriptionAsset
    {
        [Key]
        public Guid PrescriptionAssetId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid PrescriptionSettingId { get; set; }
        [MaxLength(20)]
        public string AssetType { get; set; } = null!;
        [MaxLength(255)]
        public string BlobUrl { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}