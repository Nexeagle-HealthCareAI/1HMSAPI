using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Values for the hospital's configured report-level fields (LabConfiguration.
    // ReportFieldLayoutJson's "reportFields" list) on one order -- separate from per-line results
    // (EnterPathologyResultHandler) since these apply once to the whole report, not per test.
    // Freely re-savable any time, same no-lock philosophy as the rest of the report workflow.
    public class SaveOrderReportFieldsHandler : IRequestHandler<SaveOrderReportFieldsCommand, bool>
    {
        private readonly AppDbContext _context;

        public SaveOrderReportFieldsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(SaveOrderReportFieldsCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.PathologyOrder
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);

            if (order == null)
            {
                return false;
            }

            order.ReportFieldValuesJson = request.ReportFieldValuesJson;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            _context.PathologyOrder.Update(order);

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
