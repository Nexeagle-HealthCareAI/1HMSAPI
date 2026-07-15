using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Thin request body for PublicController.UpdateReviewComment — DoctorId/ReviewId come from
    // the route instead, so they can't be spoofed independently of the URL being called.
    [ExcludeFromCodeCoverage]
    public class UpdateReviewCommentRequestBody
    {
        public string Comment { get; set; } = string.Empty;
    }
}
