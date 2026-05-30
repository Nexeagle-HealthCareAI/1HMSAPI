using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SnoozeAlertHandler : IRequestHandler<SnoozeAlertRequestModel, AlertActionResponseModel>
    {
        private readonly AppDbContext _context;

        public SnoozeAlertHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AlertActionResponseModel> Handle(SnoozeAlertRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var alert = await _context.Alert
                    .FirstOrDefaultAsync(a => a.AlertId == request.AlertId && a.HospitalId == request.HospitalId, cancellationToken);

                if (alert == null)
                    return new AlertActionResponseModel { Success = false, Message = "Alert not found." };

                alert.Status = "SNOOZED";
                alert.SnoozedUntil = request.SnoozeUntilUtc;

                await _context.SaveChangesAsync(cancellationToken);

                return new AlertActionResponseModel { Success = true, Message = "Alert snoozed.", Status = alert.Status };
            }
            catch (Exception ex)
            {
                return new AlertActionResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
