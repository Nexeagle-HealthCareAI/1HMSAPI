using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class MarkReviewHelpfulRequestModel : IRequest<MarkReviewHelpfulResponseModel>
    {
        public Guid ReviewId { get; set; }
    }
}
