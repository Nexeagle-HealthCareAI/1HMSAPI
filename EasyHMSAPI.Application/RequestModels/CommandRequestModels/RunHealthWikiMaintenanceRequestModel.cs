using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // The Health Wiki's daily upkeep (review reminders, closing stale topic requests, clearing old sign-in records).
    // Runs on its own once a day, and can be run by hand through the key-protected internal route.
    [ExcludeFromCodeCoverage]
    public class RunHealthWikiMaintenanceRequestModel : IRequest<RunHealthWikiMaintenanceResponseModel> { }
}
