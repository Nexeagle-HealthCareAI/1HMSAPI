using EasyHMSAPI.Application.Services.Models;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>Reader activity on a published article: the "Helpful" button and the view counter.</summary>
    public interface IHealthArticleStatsService
    {
        /// <summary>One like per visitor per article; asking again changes nothing.</summary>
        Task<HealthArticleReactionResult> LikeAsync(string? slug, string? visitorKey, CancellationToken cancellationToken);

        /// <summary>One view per visitor per article per day.</summary>
        Task<HealthArticleReactionResult> ViewAsync(string? slug, string? visitorKey, CancellationToken cancellationToken);
    }
}
