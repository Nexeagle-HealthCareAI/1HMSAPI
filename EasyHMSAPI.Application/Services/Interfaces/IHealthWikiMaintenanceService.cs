using EasyHMSAPI.Application.Services.Models;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// The Health Wiki's daily upkeep, safe to run any number of times a day:
    ///  - N6: a doctor who has not decided on an article for 3 days is reminded (at most 3 reminders, 3 days apart);
    ///  - a "needs detail" topic request nobody answered for 30 days is closed, and the contributor is told why;
    ///  - sign-in codes older than 2 days and sessions that ended over 30 days ago are deleted.
    /// A step that fails is logged and the others still run.
    /// </summary>
    public interface IHealthWikiMaintenanceService
    {
        Task<HealthWikiMaintenanceResult> RunAsync(CancellationToken cancellationToken);
    }
}
