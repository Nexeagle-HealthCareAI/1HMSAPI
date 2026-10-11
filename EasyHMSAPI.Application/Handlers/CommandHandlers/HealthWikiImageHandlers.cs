using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>An image from a contributor whose profile has been accepted (the same bar as writing a draft). The checks live in IHealthWikiImageService.</summary>
    public class UploadContributorImageHandler : IRequestHandler<UploadContributorImageRequestModel, UploadHealthWikiImageResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiImageService _images;

        public UploadContributorImageHandler(AppDbContext context, IHealthWikiImageService images)
        {
            _context = context;
            _images = images;
        }

        public async Task<UploadHealthWikiImageResponseModel> Handle(UploadContributorImageRequestModel request, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == request.ContributorId, ct);
            if (me == null) return UploadHealthWikiImageResponseModel.Fail(404, "Profile not found.");
            if (!ContributorPortalRules.IsProfileAccepted(me.Status))
                return UploadHealthWikiImageResponseModel.Fail(403, "Your profile has not been accepted yet. Finish registering first.");

            return ToResponse(await _images.UploadAsync(request.File, me.ContributorId, me.FullName, ct));
        }

        internal static UploadHealthWikiImageResponseModel ToResponse(HealthWikiImageResult r) =>
            new() { Success = r.Success, StatusCode = r.StatusCode, Message = r.Message, Url = r.Url };
    }

    public class UploadHealthArticleImageHandler : IRequestHandler<UploadHealthArticleImageRequestModel, UploadHealthWikiImageResponseModel>
    {
        private readonly IHealthWikiImageService _images;

        public UploadHealthArticleImageHandler(IHealthWikiImageService images)
        {
            _images = images;
        }

        public async Task<UploadHealthWikiImageResponseModel> Handle(UploadHealthArticleImageRequestModel request, CancellationToken ct) =>
            UploadContributorImageHandler.ToResponse(await _images.UploadAsync(request.File, null, HealthWikiAuditLog.ActorOrDefault(request.ActorName), ct));
    }
}
