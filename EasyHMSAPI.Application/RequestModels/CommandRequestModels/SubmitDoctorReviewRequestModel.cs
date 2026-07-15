using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class SubmitDoctorReviewRequestModel : IRequest<SubmitDoctorReviewResponseModel>
    {
        public Guid DoctorId { get; set; }
        public string? AuthorName { get; set; }
        public int Rating { get; set; }
        // Optional -- a quick "tap a star" rating can be submitted with no comment.
        public string? Comment { get; set; }
        // Set server-side by the controller from the connection, never trusted from the body.
        public string? IpAddress { get; set; }
    }
}
