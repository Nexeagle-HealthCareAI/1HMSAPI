using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    public class PrescriptionSetting
    {
        [Key]
        public Guid PrescriptionSettingId { get; set; }
        public Guid DoctorId { get; set; }
        public string? PageLayoutJson { get; set; }
        public string? LetterheadSettingsJson { get; set; }
        public string? HeaderSettingsJson { get; set; }
        public string? FooterSettingsJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}