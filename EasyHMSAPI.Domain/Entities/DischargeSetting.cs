using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A doctor+hospital's discharge-summary letterhead: an uploaded PDF background template plus
    /// margins/typography/overflow behavior. Mirrors PrescriptionSetting, minus ValidDuration (a
    /// discharge certificate has no "valid for N days" concept).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DischargeSetting
    {
        [Key]
        public Guid DischargeSettingId { get; set; }
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
