using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Renamed from CancelChargeEventHandler -- despite that old name, this has always cancelled
    // the patient's entire latest encounter and voided every charge on it, not one charge.
    public class CancelEncounterChargesHandler : IRequestHandler<CancelEncounterChargesRequestModel, CancelEncounterChargesResponseModel>
    {
        private readonly AppDbContext _context;

        public CancelEncounterChargesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CancelEncounterChargesResponseModel> Handle(CancelEncounterChargesRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Latest encounter for the patient.
                var encounter = await _context.Encounter
                    .Where(e => e.HospitalId == request.HospitalId && e.PatientId == request.PatientId)
                    .OrderByDescending(e => e.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (encounter == null)
                    return new CancelEncounterChargesResponseModel { Success = false, Message = $"No encounter found for patient {request.PatientId}." };

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

                return new CancelEncounterChargesResponseModel { Success = true, Message = "Encounter cancelled successfully." };
            }
            catch (Exception)
            {
                return new CancelEncounterChargesResponseModel { Success = false, Message = "Error cancelling encounter." };
            }
        }
    }
}
