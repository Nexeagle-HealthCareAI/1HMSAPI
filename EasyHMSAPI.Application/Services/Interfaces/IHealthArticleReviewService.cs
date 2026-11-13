using EasyHMSAPI.Application.Services.Models;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// A doctor's decision on a Health Wiki article, the same for a contributor on the Doctor Dekho pages and for a hospital
    /// doctor in EasyHMS. Only the assigned reviewer can decide; APPROVE needs the accuracy confirmation; REQUEST_CHANGES needs a
    /// comment of at least 10 characters; every decision is kept with the name and registration as they were that day.
    /// </summary>
    public interface IHealthArticleReviewService
    {
        Task<HealthArticleReviewResult> ReviewAsync(Guid reviewerContributorId, string? slug, string? decision, string? comment, bool accuracyConfirmed, CancellationToken cancellationToken);
    }
}
