using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DismissAlertHandler : IRequestHandler<DismissAlertRequestModel, AlertActionResponseModel>
    {
        private readonly AppDbContext _context;

        public DismissAlertHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AlertActionResponseModel> Handle(DismissAlertRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var alert = await _context.Alert
                    .FirstOrDefaultAsync(a => a.AlertId == request.AlertId && a.HospitalId == request.HospitalId, cancellationToken);

                if (alert == null)
                    return new AlertActionResponseModel { Success = false, Message = "Alert not found." };

                alert.Status = "DISMISSED";
                alert.DismissedAt = DateTime.UtcNow;
                alert.DismissedBy = request.LoggedInUserName;
                alert.DismissedByUserId = request.LoggedInUserId;
                alert.DismissReason = request.DismissReason;

                await _context.SaveChangesAsync(cancellationToken);

                return new AlertActionResponseModel { Success = true, Message = "Alert dismissed.", Status = alert.Status };
            }
            catch (Exception ex)
            {
                return new AlertActionResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
