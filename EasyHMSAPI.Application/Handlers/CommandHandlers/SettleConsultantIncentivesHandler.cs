using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SettleConsultantIncentivesHandler : IRequestHandler<SettleConsultantIncentivesRequestModel, SettleConsultantIncentivesResponseModel>
    {
        private readonly AppDbContext _context;

        public SettleConsultantIncentivesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SettleConsultantIncentivesResponseModel> Handle(SettleConsultantIncentivesRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty)
                    return new SettleConsultantIncentivesResponseModel { Success = false, Message = "HospitalId and DoctorId are required." };

                var query = _context.ConsultantIncentiveLedger
                    .Where(c => c.HospitalId == request.HospitalId && c.DoctorId == request.DoctorId && c.StatusCode == "ACCRUED");

                if (request.LedgerIds is { Count: > 0 })
                    query = query.Where(c => request.LedgerIds.Contains(c.ConsultantIncentiveLedgerId));

                var entries = await query.ToListAsync(cancellationToken);
                if (entries.Count == 0)
                    return new SettleConsultantIncentivesResponseModel { Success = false, Message = "No accrued entries to settle." };

                var now = DateTime.UtcNow;
                foreach (var entry in entries)
                {
                    entry.StatusCode = "PAID";
                    entry.PaidAt = now;
                    entry.PaidBy = request.LoggedInUserName;
                    entry.PayoutRef = request.PayoutRef;
                    entry.UpdatedAt = now;
                    entry.UpdatedBy = request.LoggedInUserName;
                }
                // TdsAmount is a whole-batch figure — attribute it to the first entry rather than
                // dividing it (avoids rounding-remainder drift across an arbitrary number of lines).
                if (request.TdsAmount.HasValue)
                    entries[0].TdsAmount = request.TdsAmount.Value;

                await _context.SaveChangesAsync(cancellationToken);

                return new SettleConsultantIncentivesResponseModel
                {
                    Success = true,
                    Message = "Incentives settled.",
                    SettledCount = entries.Count,
                    SettledTotal = entries.Sum(e => e.IncentiveAmount),
                };
            }
            catch (Exception)
            {
                return new SettleConsultantIncentivesResponseModel { Success = false, Message = "Error settling the consultant incentives." };
            }
        }
    }
}
