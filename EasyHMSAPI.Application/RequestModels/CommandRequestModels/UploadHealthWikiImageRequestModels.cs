using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // An image from a signed-in contributor. ContributorId comes from the session, never from the request.
    [ExcludeFromCodeCoverage]
    public class UploadContributorImageRequestModel : IRequest<UploadHealthWikiImageResponseModel>
    {
        public Guid ContributorId { get; set; }
        public IFormFile? File { get; set; }
    }

    // An image from the CMS (through the CMS API).
    [ExcludeFromCodeCoverage]
    public class UploadHealthArticleImageRequestModel : IRequest<UploadHealthWikiImageResponseModel>
    {
        public IFormFile? File { get; set; }
        public string? ActorName { get; set; }
    }
}
