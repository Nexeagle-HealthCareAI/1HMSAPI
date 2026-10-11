using EasyHMSAPI.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>
    /// What a message needs to say about an article: its id, its address and the title to show. The title is passed in because a live
    /// article's pending edit can carry a newer one than the article itself.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public record HealthArticleRef(Guid ArticleId, string Slug, string Title)
    {
        public static HealthArticleRef From(HealthArticle article, string? title = null) => new(article.ArticleId, article.Slug, title ?? article.Title);
    }
}
