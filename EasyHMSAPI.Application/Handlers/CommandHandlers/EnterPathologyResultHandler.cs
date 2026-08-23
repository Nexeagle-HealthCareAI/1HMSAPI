using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class EnterPathologyResultHandler : IRequestHandler<EnterPathologyResultCommand, bool>
    {
        private readonly AppDbContext _context;

        public EnterPathologyResultHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(EnterPathologyResultCommand request, CancellationToken cancellationToken)
        {
            var line = await _context.PathologyOrderLine
                .Where(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId && l.OrderLineId == request.OrderLineId)
                .FirstOrDefaultAsync(cancellationToken);

            if (line == null)
            {
                return false;
            }

            var result = await _context.PathologyResult
                .Where(r => r.OrderLineId == line.OrderLineId)
                .FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                result = new PathologyResult
                {
                    ResultId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    OrderLineId = line.OrderLineId,
                    ResultValuesJson = request.ResultValuesJson,
                    Interpretation = request.Interpretation,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserId.ToString()
                };
                _context.PathologyResult.Add(result);
            }
            else
            {
                result.ResultValuesJson = request.ResultValuesJson;
                result.Interpretation = request.Interpretation;
                result.UpdatedAt = DateTime.UtcNow;
                result.UpdatedBy = request.LoggedInUserId.ToString();
                _context.PathologyResult.Update(result);
            }

            // Update line status
            if (line.Status == "PENDING" || line.Status == "SAMPLE_COLLECTED")
            {
                line.Status = "RESULT_ENTERED";
                line.UpdatedAt = DateTime.UtcNow;
                line.UpdatedBy = request.LoggedInUserId.ToString();
                _context.PathologyOrderLine.Update(line);
            }

            // Check if all lines are completed to update order status
            var allLines = await _context.PathologyOrderLine
                .Where(l => l.OrderId == request.OrderId)
                .ToListAsync(cancellationToken);
                
            var order = await _context.PathologyOrder
                .Where(o => o.OrderId == request.OrderId)
                .FirstOrDefaultAsync(cancellationToken);

            if (order != null)
            {
                bool allDone = allLines.All(l => l.Status == "RESULT_ENTERED" || l.Status == "REPORT_APPROVED" || (l.OrderLineId == line.OrderLineId && line.Status == "RESULT_ENTERED"));
                if (allDone && order.Status != "COMPLETED")
                {
                    order.Status = "COMPLETED"; // Or leave as IN_PROGRESS until report is approved depending on business rules
                    order.UpdatedAt = DateTime.UtcNow;
                    order.UpdatedBy = request.LoggedInUserId.ToString();
                    _context.PathologyOrder.Update(order);
                }
                else if (order.Status == "PLACED")
                {
                    order.Status = "IN_PROGRESS";
                    order.UpdatedAt = DateTime.UtcNow;
                    order.UpdatedBy = request.LoggedInUserId.ToString();
                    _context.PathologyOrder.Update(order);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
