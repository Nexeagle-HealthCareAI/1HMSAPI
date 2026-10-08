using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // A CMS editor's decision on an article.
    //   APPROVE   - publish a SECTOR_UPDATE (or apply its pending edit). A MEDICAL article is approved by its doctor, not here.
    //   WITHDRAW  - send back for changes; Reason is shown to the author. Does not touch what is live.
    //   UNPUBLISH - take a live article offline at once; Reason is recorded.
    [ExcludeFromCodeCoverage]
    public class DecideHealthArticleRequestModel : IRequest<SaveHealthArticleResponseModel>
    {
        public const string Approve = "APPROVE";
        public const string Withdraw = "WITHDRAW";
        public const string Unpublish = "UNPUBLISH";

        public string Slug { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? Reason { get; set; }
        public string? ActorName { get; set; }
    }
}
