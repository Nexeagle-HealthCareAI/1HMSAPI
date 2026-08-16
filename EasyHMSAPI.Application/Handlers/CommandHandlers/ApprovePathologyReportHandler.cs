using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class ApprovePathologyReportHandler : IRequestHandler<ApprovePathologyReportCommand, bool>
    {
        private readonly AppDbContext _context;

        public ApprovePathologyReportHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(ApprovePathologyReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _context.PathologyReport
                .FirstOrDefaultAsync(r => r.ReportId == request.ReportId && r.HospitalId == request.HospitalId, cancellationToken);

            if (report == null)
            {
                throw new ApplicationException("Pathology report not found.");
            }

            if (report.Status == "APPROVED")
            {
                throw new ApplicationException("This report has already been approved.");
            }

            var now = DateTime.UtcNow;

            // 1. Approve the report
            report.Status = "APPROVED";
            report.ApprovedAt = now;
            report.ApprovedByUserId = request.LoggedInUserId;
            report.UpdatedAt = now;
            report.UpdatedBy = request.LoggedInUserName ?? "System";
            _context.PathologyReport.Update(report);

            // 2. Update all linked order lines to REPORT_APPROVED
            var resultOrderLineIds = await _context.PathologyResult
                .Where(r => r.ReportId == request.ReportId && r.HospitalId == request.HospitalId)
                .Select(r => r.OrderLineId)
                .ToListAsync(cancellationToken);

            var orderLines = await _context.PathologyOrderLine
                .Where(l => resultOrderLineIds.Contains(l.OrderLineId) && l.HospitalId == request.HospitalId)
                .ToListAsync(cancellationToken);

            foreach (var line in orderLines)
            {
                line.Status = "REPORT_APPROVED";
                line.ReportId = report.ReportId;
                line.UpdatedAt = now;
                line.UpdatedBy = request.LoggedInUserName ?? "System";
                _context.PathologyOrderLine.Update(line);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
