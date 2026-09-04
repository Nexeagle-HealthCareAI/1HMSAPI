using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Marks that the external lab's result has physically/digitally arrived back -- staff still enter
    // the actual values afterwards via the normal EnterPathologyResultHandler flow (unchanged, it
    // accepts a line in any pre-RESULT_ENTERED status). This step exists purely so the workspace can
    // show "awaiting external lab" vs "result received, needs entry" instead of the two being
    // indistinguishable while a line sits outsourced.
    public class ReceivePathologyExternalLabResultHandler : IRequestHandler<ReceivePathologyExternalLabResultCommand, bool>
    {
        private readonly AppDbContext _context;

        public ReceivePathologyExternalLabResultHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(ReceivePathologyExternalLabResultCommand request, CancellationToken cancellationToken)
        {
            var line = await _context.PathologyOrderLine
                .FirstOrDefaultAsync(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId && l.OrderLineId == request.OrderLineId, cancellationToken);
            if (line == null || line.Status != "SENT_TO_EXTERNAL_LAB")
            {
                return false;
            }

            var now = DateTime.UtcNow;
            line.ExternalLabReceivedAt = now;
            line.Status = "RESULT_RECEIVED_FROM_EXTERNAL_LAB";
            line.UpdatedAt = now;
            line.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            _context.PathologyOrderLine.Update(line);

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
