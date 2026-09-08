using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Marks an outsourced test's line as sent to a third-party lab. Only valid for a line whose test
    // is flagged IsOutsourced, and only once its sample has actually been collected in-house (the
    // in-house SAMPLE_COLLECTED step is unaffected by outsourcing -- specimens still get drawn here
    // before being couriered out). Snapshots PathologyTestMaster.CostPrice onto the line at send
    // time, same pattern BillingChargeEvent already uses for charge rates, so a later catalog cost
    // edit can't retroactively change an already-sent line's recorded cost.
    public class SendPathologyLineToExternalLabHandler : IRequestHandler<SendPathologyLineToExternalLabCommand, bool>
    {
        private readonly AppDbContext _context;

        public SendPathologyLineToExternalLabHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(SendPathologyLineToExternalLabCommand request, CancellationToken cancellationToken)
        {
            var line = await _context.PathologyOrderLine
                .FirstOrDefaultAsync(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId && l.OrderLineId == request.OrderLineId, cancellationToken);
            if (line == null || line.Status != "SAMPLE_COLLECTED")
            {
                return false;
            }

            var test = await _context.PathologyTestMaster
                .FirstOrDefaultAsync(t => t.HospitalId == request.HospitalId && t.TestId == line.TestId, cancellationToken);
            if (test == null || !test.IsOutsourced)
            {
                return false;
            }

            var externalLabId = request.ExternalLabId ?? test.DefaultExternalLabId;
            if (!externalLabId.HasValue)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            line.ExternalLabId = externalLabId;
            line.SentToExternalLabAt = now;
            line.ExternalLabRefNo = string.IsNullOrWhiteSpace(request.ExternalLabRefNo) ? null : request.ExternalLabRefNo.Trim();
            line.ExternalLabCost = test.CostPrice;
            line.Status = "SENT_TO_EXTERNAL_LAB";
            line.UpdatedAt = now;
            line.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            _context.PathologyOrderLine.Update(line);

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
