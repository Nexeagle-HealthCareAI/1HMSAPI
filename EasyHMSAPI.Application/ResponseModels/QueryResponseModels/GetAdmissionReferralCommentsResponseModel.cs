using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferralCommentsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AdmissionReferralCommentItem> Comments { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionReferralCommentItem
    {
        public Guid CommentId { get; set; }
        public string CommentText { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
