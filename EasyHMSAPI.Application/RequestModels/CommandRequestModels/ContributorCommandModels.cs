using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Ask for a WhatsApp code. With InviteToken the code goes only to the number the link was sent to.
    [ExcludeFromCodeCoverage]
    public class ContributorOtpSendRequestModel : IRequest<ContributorOtpSendResponseModel>
    {
        public string? Mobile { get; set; }
        public string? InviteToken { get; set; }
        public string? RequestIp { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ContributorOtpVerifyRequestModel : IRequest<ContributorOtpVerifyResponseModel>
    {
        public string? Mobile { get; set; }
        public string? Code { get; set; }
        public string? InviteToken { get; set; }
        // Only for someone joining without an invitation: INDEPENDENT_DOCTOR | HEALTH_WORKER | WRITER.
        public string? Role { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ContributorLogoutRequestModel : IRequest<ContributorSimpleResponseModel>
    {
        public Guid SessionId { get; set; }
    }

    // CMS: invite someone to contribute. Creates the contributor (INVITED) and sends a JOIN link.
    [ExcludeFromCodeCoverage]
    public class InviteContributorRequestModel : IRequest<ContributorAdminResponseModel>
    {
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        // INDEPENDENT_DOCTOR | HEALTH_WORKER | WRITER
        public string? Type { get; set; }
        public string? ActorName { get; set; }
    }

    // CMS: send (or resend) a link. With ArticleSlug it is the review or write link for that article, otherwise a JOIN link.
    [ExcludeFromCodeCoverage]
    public class SendContributorLinkRequestModel : IRequest<ContributorAdminResponseModel>
    {
        public Guid ContributorId { get; set; }
        public string? ArticleSlug { get; set; }
        public string? ActorName { get; set; }
    }

    // CMS: verify a doctor's registration, or approve a health worker or writer.
    [ExcludeFromCodeCoverage]
    public class VerifyContributorRequestModel : IRequest<ContributorAdminResponseModel>
    {
        public Guid ContributorId { get; set; }
        public string? ActorName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RejectContributorRequestModel : IRequest<ContributorAdminResponseModel>
    {
        public Guid ContributorId { get; set; }
        public string? Reason { get; set; }
        public string? ActorName { get; set; }
    }
}
