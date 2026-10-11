using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // The record of WhatsApp messages the Health Wiki sent, newest first, with what WhatsApp later said about each.
    [ExcludeFromCodeCoverage]
    public class GetHealthWikiNotificationsRequestModel : IRequest<GetHealthWikiNotificationsResponseModel>
    {
        // At most 200.
        public int Take { get; set; } = 50;
        // Only messages that were refused, skipped or reported as not delivered.
        public bool ProblemsOnly { get; set; }
    }
}
