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
        public string Comment { get; set; } = string.Empty;
        // Set server-side by the controller from the connection, never trusted from the body.
        public string? IpAddress { get; set; }
    }
}
