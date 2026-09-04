using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Cancels an order as a whole -- there is no per-test-line cancel; if only one test on a
    // multi-test order needs dropping, cancel and re-place is the supported path for now. Blocked
    // once any line already has a report (that test's result has effectively already been
    // delivered), so this only ever undoes work that hasn't been finalized yet.
    public class CancelPathologyOrderHandler : IRequestHandler<CancelPathologyOrderCommand, CancelPathologyOrderResponseModel>
    {
        private readonly AppDbContext _context;

        public CancelPathologyOrderHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CancelPathologyOrderResponseModel> Handle(CancelPathologyOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.PathologyOrder
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);
            if (order == null)
            {
                return new CancelPathologyOrderResponseModel { Success = false, Message = "Order not found." };
            }
            if (order.Status == "CANCELLED")
            {
                return new CancelPathologyOrderResponseModel { Success = false, Message = "This order is already cancelled." };
            }

            var hasReport = await _context.PathologyOrderLine
                .AnyAsync(l => l.OrderId == request.OrderId && l.HospitalId == request.HospitalId && l.ReportId != null, cancellationToken);
            if (hasReport)
            {
                return new CancelPathologyOrderResponseModel { Success = false, Message = "Cannot cancel -- a report has already been generated for at least one test on this order." };
            }

            var now = DateTime.UtcNow;
            var actor = request.LoggedInUserName ?? request.LoggedInUserId.ToString();

            order.Status = "CANCELLED";
            order.UpdatedAt = now;
            order.UpdatedBy = actor;
            _context.PathologyOrder.Update(order);

            // Void only this order's own charges (matched by SourceRefId) -- not the whole
            // encounter, which may carry unrelated charges (other tests, medications, etc.). Matches
            // both the legacy bare-order-id format and the current per-line "{orderId}:{testId}"
            // format (see PathologyAutoBillingHelper.BuildChargeDetailsAsync) since this voids
            // everything for the order regardless of which test each charge belongs to.
            var orderIdStr = order.OrderId.ToString();
            var orderIdPrefix = orderIdStr + ":";
            var charges = await _context.BillingChargeEvent
                .Where(c => c.SourceModule == BillingConstants.SourceModule.LabPath
                    && (c.SourceRefId == orderIdStr || (c.SourceRefId != null && c.SourceRefId.StartsWith(orderIdPrefix)))
                    && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                .ToListAsync(cancellationToken);
            foreach (var charge in charges)
            {
                charge.StatusCode = BillingConstants.ChargeEventStatus.Void;
                charge.VoidedAt = now;
                charge.VoidedBy = actor;
                charge.VoidReason = "Pathology order cancelled";
                charge.UpdatedAt = now;
                charge.UpdatedBy = actor;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new CancelPathologyOrderResponseModel { Success = true };
        }
    }
}
