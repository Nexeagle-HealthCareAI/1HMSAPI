using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DoctorReview
    {
        [Key]
        public Guid ReviewId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? AuthorName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = null!;
        public int HelpfulCount { get; set; }
        public bool IsHidden { get; set; }
        public string? SubmittedIp { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Doctor Doctor { get; set; } = null!;
    }
}
