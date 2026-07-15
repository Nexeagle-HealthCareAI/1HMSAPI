using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateReviewCommentRequestModel : IRequest<UpdateReviewCommentResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid ReviewId { get; set; }
        public string Comment { get; set; } = string.Empty;
    }
}
