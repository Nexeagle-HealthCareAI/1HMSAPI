using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AddAdmissionReferralCommentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? CommentId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
