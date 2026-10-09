using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // What the invitation page shows before the person signs in. Never the full mobile number.
    [ExcludeFromCodeCoverage]
    public class ContributorInviteInfo
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // REVIEW | WRITE | JOIN
        public string? Kind { get; set; }
        // INDEPENDENT_DOCTOR | HEALTH_WORKER | WRITER
        public string? Role { get; set; }
        public string? InviterName { get; set; }
        public string? MaskedMobile { get; set; }
        public string? ArticleTitle { get; set; }
        public string? ArticleCoverUrl { get; set; }
        public string? InviteeName { get; set; }
    }

    // A contributor as the CMS sees it. The CMS (staff) may see the mobile number; no public endpoint returns it.
    [ExcludeFromCodeCoverage]
    public class ContributorAdminInfo
    {
        public Guid ContributorId { get; set; }
        public string Type { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Speciality { get; set; }
        public string? Qualification { get; set; }
        public string? RoleTitle { get; set; }
        public string? Organisation { get; set; }
        public string? Mobile { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegistrationCouncil { get; set; }
        public string Status { get; set; } = null!;
        public string EnrolmentSource { get; set; } = null!;
        public DateTime? LinkSentAt { get; set; }
        public string? RejectReason { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorsAdminResponseModel
    {
        public List<ContributorAdminInfo> Contributors { get; set; } = new();
    }
}
