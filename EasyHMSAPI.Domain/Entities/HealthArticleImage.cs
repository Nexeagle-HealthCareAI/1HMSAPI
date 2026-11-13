using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // An image uploaded for a Health Wiki article. The file is in the Health Wiki bucket; this row records who sent it and when.
    // ArticleId stays empty until the image is used in a saved article.
    [ExcludeFromCodeCoverage]
    [Table("HealthArticleImage")]
    public class HealthArticleImage
    {
        [Key]
        public Guid ImageId { get; set; }
        public Guid? ArticleId { get; set; }
        public Guid? ContributorId { get; set; }
        public string? UploadedByName { get; set; }
        public string Url { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public int SizeBytes { get; set; }
        public string? AltText { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
