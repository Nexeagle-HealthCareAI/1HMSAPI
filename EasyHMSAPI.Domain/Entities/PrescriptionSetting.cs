using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PrescriptionSetting
    {
        [Key]
        public Guid PrescriptionSettingId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public int? HeaderHeight { get; set; }
        public int? FooterHeight { get; set; }
        public int? ContentLeftMargin { get; set; }
        public int? ContentRightMargin { get; set; }
        public bool? OverFlowPage { get; set; }
        [MaxLength(100)]
        public string? FontFamily { get; set; }
        public int? FontSize { get; set; }
        [MaxLength(50)]
        public string? FontWeight { get; set; }
        [MaxLength(50)]
        public string? TextColour { get; set; }
        [MaxLength(2048)]
        public string? URI { get; set; }
        public Guid? CreatedByUserId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}