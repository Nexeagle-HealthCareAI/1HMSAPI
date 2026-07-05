using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetMaintenanceLogHistoryHandler : IRequestHandler<GetMaintenanceLogHistoryRequestModel, GetMaintenanceLogHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetMaintenanceLogHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMaintenanceLogHistoryResponseModel> Handle(GetMaintenanceLogHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var logs = await _context.MaintenanceLog
                .Where(m => m.HospitalId == request.HospitalId && m.EquipmentId == request.EquipmentId)
                .OrderByDescending(m => m.PerformedAt)
                .Select(m => new MaintenanceLogDataModel
                {
                    MaintenanceLogId = m.MaintenanceLogId,
                    ActivityType = m.ActivityType,
                    PerformedAt = m.PerformedAt,
                    PerformedBy = m.PerformedBy,
                    VendorName = m.VendorName,
                    Cost = m.Cost,
                    PartsReplaced = m.PartsReplaced,
                    Findings = m.Findings,
                    ActionTaken = m.ActionTaken,
                    Outcome = m.Outcome,
                    NextDueAtOverride = m.NextDueAtOverride,
                    Notes = m.Notes,
                })
                .ToListAsync(cancellationToken);

            return new GetMaintenanceLogHistoryResponseModel { Logs = logs };
        }
    }
}
