using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>The Health Wiki's daily upkeep. The work is in IHealthWikiMaintenanceService.</summary>
    public class RunHealthWikiMaintenanceHandler : IRequestHandler<RunHealthWikiMaintenanceRequestModel, RunHealthWikiMaintenanceResponseModel>
    {
        private readonly IHealthWikiMaintenanceService _maintenance;

        public RunHealthWikiMaintenanceHandler(IHealthWikiMaintenanceService maintenance)
        {
            _maintenance = maintenance;
        }

        public async Task<RunHealthWikiMaintenanceResponseModel> Handle(RunHealthWikiMaintenanceRequestModel request, CancellationToken ct)
        {
            var r = await _maintenance.RunAsync(ct);
            return new RunHealthWikiMaintenanceResponseModel
            {
                Success = true, RemindersSent = r.RemindersSent, RemindersNotSent = r.RemindersNotSent, TopicsClosed = r.TopicsClosed,
                OtpsRemoved = r.OtpsRemoved, SessionsRemoved = r.SessionsRemoved,
            };
        }
    }
}
