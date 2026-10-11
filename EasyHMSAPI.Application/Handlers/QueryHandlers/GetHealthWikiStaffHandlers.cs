using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// A hospital doctor's Health Wiki inbox: articles waiting for their review, and the ones they approved. The doctor's contributor row is
    /// made on first use (see IHealthWikiStaffService). An article they approved before being verified is listed as published but not live.
    /// </summary>
    public class GetHealthWikiInboxHandler : IRequestHandler<GetHealthWikiInboxRequestModel, GetHealthWikiInboxResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiStaffService _staff;

        public GetHealthWikiInboxHandler(AppDbContext context, IHealthWikiStaffService staff)
        {
            _context = context;
            _staff = staff;
        }

        public async Task<GetHealthWikiInboxResponseModel> Handle(GetHealthWikiInboxRequestModel request, CancellationToken ct)
        {
            var me = await _staff.EnsureContributorAsync(request.UserId, ct);
            if (me == null) return GetHealthWikiInboxResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            if (me.Status == HealthWikiContributor.StatusRejected) return GetHealthWikiInboxResponseModel.Fail(403, "Your Health Wiki access has been removed.");

            var mine = (await ContributorArticleLoader.Load(_context, me.ContributorId, null, ct)).Where(a => a.MyRole == ContributorPortalMapper.RoleReviewer).ToList();
            return new GetHealthWikiInboxResponseModel
            {
                Success = true,
                Pending = mine.Where(a => !a.AwaitingVerification && (a.Status == HealthArticle.StatusInReview || a.RevisionStatus == HealthArticleRevision.StatusInReview))
                              .OrderByDescending(a => a.WaitingDays ?? 0).Select(ToItem).ToList(),
                Published = mine.Where(a => a.Status == HealthArticle.StatusPublished).OrderByDescending(a => a.UpdatedAt).Select(ToItem).ToList(),
            };
        }

        internal static HealthWikiInboxItem ToItem(MyArticleInfo a) => new()
        {
            Slug = a.Slug, Type = a.Type, Title = a.Title, Description = a.Description, CoverImageUrl = a.CoverImageUrl, AuthorName = a.AuthorName,
            WaitingDays = a.WaitingDays, Views = a.Views, Likes = a.Likes, PublishedAt = a.PublishedAt, UpdatedAt = a.UpdatedAt,
        };
    }

    /// <summary>One article for the assigned doctor: the text to review, their badge, whether the decision is theirs now, and the 14-day views.</summary>
    public class GetHealthWikiStaffArticleHandler : IRequestHandler<GetHealthWikiStaffArticleRequestModel, GetHealthWikiStaffArticleResponseModel>
    {
        private const int ChartDays = 14;

        private readonly AppDbContext _context;
        private readonly IHealthWikiStaffService _staff;

        public GetHealthWikiStaffArticleHandler(AppDbContext context, IHealthWikiStaffService staff)
        {
            _context = context;
            _staff = staff;
        }

        public async Task<GetHealthWikiStaffArticleResponseModel> Handle(GetHealthWikiStaffArticleRequestModel request, CancellationToken ct)
        {
            var me = await _staff.EnsureContributorAsync(request.UserId, ct);
            if (me == null) return GetHealthWikiStaffArticleResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            if (me.Status == HealthWikiContributor.StatusRejected) return GetHealthWikiStaffArticleResponseModel.Fail(403, "Your Health Wiki access has been removed.");

            var slug = request.Slug?.Trim().ToLowerInvariant();
            var a = (await ContributorArticleLoader.Load(_context, me.ContributorId, slug, ct)).FirstOrDefault(x => x.MyRole == ContributorPortalMapper.RoleReviewer);
            if (a == null) return GetHealthWikiStaffArticleResponseModel.Fail(404, "Article not found.");

            var editInReview = a.Status == HealthArticle.StatusPublished && a.RevisionStatus == HealthArticleRevision.StatusInReview;
            var withDoctor = !a.AwaitingVerification && (a.Status == HealthArticle.StatusInReview || editInReview);
            // Approving an edit to a live article needs a verified registration (the review service enforces it; this hides the buttons).
            var canDecide = withDoctor && (!editInReview || me.Status == HealthWikiContributor.StatusVerified);
            var status = withDoctor ? HealthArticle.StatusInReview : HealthArticle.StatusPublished;

            var daily = new List<int>();
            if (status == HealthArticle.StatusPublished && !a.AwaitingVerification)
            {
                var articleId = await _context.HealthArticles.AsNoTracking().Where(x => x.Slug == slug).Select(x => x.ArticleId).FirstAsync(ct);
                var today = DateTime.UtcNow.Date;
                var from = today.AddDays(-(ChartDays - 1));
                var rows = await _context.HealthArticleStatDailies.AsNoTracking().Where(d => d.ArticleId == articleId && d.StatDate >= from).ToListAsync(ct);
                for (var i = 0; i < ChartDays; i++) daily.Add(rows.FirstOrDefault(r => r.StatDate.Date == from.AddDays(i))?.Views ?? 0);
            }

            var info = new HealthWikiStaffArticleInfo
            {
                Slug = a.Slug, Type = a.Type, Title = a.Title, Description = a.Description, CoverImageUrl = a.CoverImageUrl, AuthorName = a.AuthorName,
                WaitingDays = a.WaitingDays, Views = a.Views, Likes = a.Likes, PublishedAt = a.PublishedAt, UpdatedAt = a.UpdatedAt,
                Content = a.Content, CoverImageAlt = a.CoverImageAlt, RelatedConditionSlug = a.RelatedConditionSlug, Disclosure = a.Disclosure, References = a.References,
                Reviewer = new HealthWikiReviewerBadge
                {
                    Name = me.FullName, Qualification = me.Qualification, Speciality = me.Speciality,
                    RegistrationNumber = me.RegistrationNumber, RegistrationCouncil = me.RegistrationCouncil,
                },
                Status = status, CanDecide = canDecide, DailyViews = daily,
            };
            return new GetHealthWikiStaffArticleResponseModel { Success = true, Article = info };
        }
    }
}
