using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Views and likes of one article on one day (UTC), for the 14-day chart the reviewing doctor sees.
    [ExcludeFromCodeCoverage]
    [Table("HealthArticleStatDaily")]
    public class HealthArticleStatDaily
    {
        public Guid ArticleId { get; set; }
        public DateTime StatDate { get; set; }
        public int Views { get; set; }
        public int Likes { get; set; }
    }
}
