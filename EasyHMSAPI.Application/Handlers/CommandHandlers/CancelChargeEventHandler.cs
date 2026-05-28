using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CancelChargeEventHandler : IRequestHandler<CancelChargeEventRequestModel, CancelChargeEventResponseModel>
    {
        private readonly AppDbContext _context;

        public CancelChargeEventHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CancelChargeEventResponseModel> Handle(CancelChargeEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Latest encounter for the patient.
                var encounter = await _context.Encounter
                    .Where(e => e.HospitalId == request.HospitalId && e.PatientId == request.PatientId)
                    .OrderByDescending(e => e.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (encounter == null)
                    return new CancelChargeEventResponseModel { Success = false, Message = $"No encounter found for patient {request.PatientId}." };

                encounter.StatusCode = BillingConstants.EncounterStatus.Cancelled;
                encounter.UpdatedAt = DateTime.UtcNow;
                encounter.UpdatedBy = request.LoggedInUserName;

                // Void all non-void charge events on this encounter.
                var chargeEvents = await _context.BillingChargeEvent
                    .Where(c => c.EncounterId == encounter.EncounterId && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                    .ToListAsync(cancellationToken);

                foreach (var ce in chargeEvents)
                {
                    ce.StatusCode = BillingConstants.ChargeEventStatus.Void;
                    ce.VoidedAt = DateTime.UtcNow;
                    ce.VoidedBy = request.LoggedInUserName;
                    ce.VoidReason = request.CancelReason;
                    ce.UpdatedAt = DateTime.UtcNow;
                    ce.UpdatedBy = request.LoggedInUserName;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new CancelChargeEventResponseModel { Success = true, Message = "Encounter cancelled successfully." };
            }
            catch (Exception)
            {
                return new CancelChargeEventResponseModel { Success = false, Message = "Error cancelling encounter." };
            }
        }
    }
}
