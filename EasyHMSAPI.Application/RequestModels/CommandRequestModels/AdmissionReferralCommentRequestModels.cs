using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Adds a timestamped, author-attributed comment against a Referred Admissions board row.
    // Insert-only -- no edit/delete surfaced anywhere, matching the ask ("add a comment ... and see
    // when added").
    [ExcludeFromCodeCoverage]
    public class AddAdmissionReferralCommentRequestModel : IRequest<AddAdmissionReferralCommentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid ReferralId { get; set; }
        public string CommentText { get; set; } = null!;
    }
}
