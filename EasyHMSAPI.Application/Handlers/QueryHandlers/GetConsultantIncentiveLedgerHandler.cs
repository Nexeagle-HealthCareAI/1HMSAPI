using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetConsultantIncentiveLedgerHandler : IRequestHandler<GetConsultantIncentiveLedgerRequestModel, GetConsultantIncentiveLedgerResponseModel>
    {
        private readonly AppDbContext _context;

        public GetConsultantIncentiveLedgerHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetConsultantIncentiveLedgerResponseModel> Handle(GetConsultantIncentiveLedgerRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty)
                    return new GetConsultantIncentiveLedgerResponseModel { Success = false, Message = "HospitalId and DoctorId are required." };

                var query = _context.ConsultantIncentiveLedger
                    .Where(c => c.HospitalId == request.HospitalId && c.DoctorId == request.DoctorId);

                if (!string.IsNullOrWhiteSpace(request.StatusCode))
                    query = query.Where(c => c.StatusCode == request.StatusCode.Trim().ToUpperInvariant());
                if (request.FromDate.HasValue)
                    query = query.Where(c => c.AccruedAt >= request.FromDate.Value);
                if (request.ToDate.HasValue)
                    query = query.Where(c => c.AccruedAt <= request.ToDate.Value);

                var rows = await query
                    .Join(_context.BillingChargeEvent, c => c.ChargeEventId, e => e.ChargeEventId, (c, e) => new { c, e.DisplayName })
                    .OrderByDescending(x => x.c.AccruedAt)
                    .ToListAsync(cancellationToken);

                var lines = rows.Select(x => new ConsultantIncentiveLineModel
                {
                    ConsultantIncentiveLedgerId = x.c.ConsultantIncentiveLedgerId,
                    PatientId = x.c.PatientId,
                    ChargeEventId = x.c.ChargeEventId,
                    ChargeDisplayName = x.DisplayName,
                    IncentiveAmount = x.c.IncentiveAmount,
                    StatusCode = x.c.StatusCode,
                    AccruedAt = x.c.AccruedAt,
                    PaidAt = x.c.PaidAt,
                    PayoutRef = x.c.PayoutRef,
                    TdsAmount = x.c.TdsAmount,
                }).ToList();

                return new GetConsultantIncentiveLedgerResponseModel
                {
                    Success = true,
                    Lines = lines,
                    AccruedTotal = lines.Where(l => l.StatusCode == "ACCRUED").Sum(l => l.IncentiveAmount),
                    PaidTotal = lines.Where(l => l.StatusCode == "PAID").Sum(l => l.IncentiveAmount),
                    CancelledTotal = lines.Where(l => l.StatusCode == "CANCELLED").Sum(l => l.IncentiveAmount),
                };
            }
            catch (Exception)
            {
                return new GetConsultantIncentiveLedgerResponseModel { Success = false, Message = "Error loading the consultant incentive ledger." };
            }
        }
    }
}
