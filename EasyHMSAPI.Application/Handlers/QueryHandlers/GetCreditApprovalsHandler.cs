using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetCreditApprovalsHandler : IRequestHandler<GetCreditApprovalsRequestModel, GetCreditApprovalsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetCreditApprovalsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetCreditApprovalsResponseModel> Handle(GetCreditApprovalsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.CreditApproval.AsNoTracking().Where(a => a.HospitalId == request.HospitalId);

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(a => a.Status == request.Status);
            }
            if (request.EncounterId.HasValue && request.EncounterId != Guid.Empty)
            {
                query = query.Where(a => a.EncounterId == request.EncounterId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.PatientId))
            {
                query = query.Where(a => a.PatientId == request.PatientId);
            }

            query = query.OrderByDescending(a => a.RequestedAt);
            var take = request.Take.GetValueOrDefault(200);
            if (take > 0)
            {
                query = query.Take(take);
            }

            var items = await query.Select(a => new CreditApprovalItem
            {
                CreditApprovalId = a.CreditApprovalId,
                EncounterId = a.EncounterId,
                PatientId = a.PatientId,
                PaymentType = a.PaymentType,
                RequestedAmount = a.RequestedAmount,
                PaymentMode = a.PaymentMode,
                ResultingCreditBalance = a.ResultingCreditBalance,
                Reason = a.Reason,
                RequestedBy = a.RequestedBy,
                RequestedAt = a.RequestedAt,
                Status = a.Status,
                DecidedAt = a.DecidedAt,
                DecidedBy = a.DecidedBy,
                DecisionNote = a.DecisionNote,
            }).ToListAsync(cancellationToken);

            return new GetCreditApprovalsResponseModel { Success = true, Items = items };
        }
    }
}
