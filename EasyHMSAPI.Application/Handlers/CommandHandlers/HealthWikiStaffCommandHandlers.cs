using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>A hospital doctor's decision in EasyHMS. The same review rules as on the Doctor Dekho pages (IHealthArticleReviewService).</summary>
    public class ReviewHealthWikiArticleHandler : IRequestHandler<ReviewHealthWikiArticleRequestModel, ReviewHealthWikiArticleResponseModel>
    {
        private readonly IHealthWikiStaffService _staff;
        private readonly IHealthArticleReviewService _reviews;

        public ReviewHealthWikiArticleHandler(IHealthWikiStaffService staff, IHealthArticleReviewService reviews)
        {
            _staff = staff;
            _reviews = reviews;
        }

        public async Task<ReviewHealthWikiArticleResponseModel> Handle(ReviewHealthWikiArticleRequestModel request, CancellationToken ct)
        {
            var me = await _staff.EnsureContributorAsync(request.UserId, ct);
            if (me == null) return ReviewHealthWikiArticleResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            if (me.Status == HealthWikiContributor.StatusRejected) return ReviewHealthWikiArticleResponseModel.Fail(403, "Your Health Wiki access has been removed.");

            var result = await _reviews.ReviewAsync(me.ContributorId, request.Slug, request.Decision, request.Comment, request.AccuracyConfirmed, ct);
            return result.Success
                ? new ReviewHealthWikiArticleResponseModel { Success = true, StatusCode = 200, Outcome = result.Outcome }
                : ReviewHealthWikiArticleResponseModel.Fail(result.StatusCode, result.Message!);
        }
    }

    public class LikeHealthArticleHandler : IRequestHandler<LikeHealthArticleRequestModel, HealthArticleReactionResponseModel>
    {
        private readonly IHealthArticleStatsService _stats;

        public LikeHealthArticleHandler(IHealthArticleStatsService stats)
        {
            _stats = stats;
        }

        public async Task<HealthArticleReactionResponseModel> Handle(LikeHealthArticleRequestModel request, CancellationToken ct) =>
            ToResponse(await _stats.LikeAsync(request.Slug, request.VisitorKey, ct));

        internal static HealthArticleReactionResponseModel ToResponse(HealthArticleReactionResult r) => new()
        {
            Success = r.Success, StatusCode = r.StatusCode, Message = r.Message, Changed = r.Changed, Views = r.Views, Likes = r.Likes,
        };
    }

    public class RecordHealthArticleViewHandler : IRequestHandler<RecordHealthArticleViewRequestModel, HealthArticleReactionResponseModel>
    {
        private readonly IHealthArticleStatsService _stats;

        public RecordHealthArticleViewHandler(IHealthArticleStatsService stats)
        {
            _stats = stats;
        }

        public async Task<HealthArticleReactionResponseModel> Handle(RecordHealthArticleViewRequestModel request, CancellationToken ct) =>
            LikeHealthArticleHandler.ToResponse(await _stats.ViewAsync(request.Slug, request.VisitorKey, ct));
    }
}
