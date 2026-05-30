using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class AcknowledgeAlertHandler : IRequestHandler<AcknowledgeAlertRequestModel, AlertActionResponseModel>
    {
        private readonly AppDbContext _context;

        public AcknowledgeAlertHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AlertActionResponseModel> Handle(AcknowledgeAlertRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var alert = await _context.Alert
                    .FirstOrDefaultAsync(a => a.AlertId == request.AlertId && a.HospitalId == request.HospitalId, cancellationToken);

                if (alert == null)
                    return new AlertActionResponseModel { Success = false, Message = "Alert not found." };

                alert.Status = "ACKNOWLEDGED";
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.AcknowledgedBy = request.LoggedInUserName;
                alert.AcknowledgedByUserId = request.LoggedInUserId;
                alert.AcknowledgeNote = request.AcknowledgeNote;

                await _context.SaveChangesAsync(cancellationToken);

                return new AlertActionResponseModel { Success = true, Message = "Alert acknowledged.", Status = alert.Status };
            }
            catch (Exception ex)
            {
                return new AlertActionResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
