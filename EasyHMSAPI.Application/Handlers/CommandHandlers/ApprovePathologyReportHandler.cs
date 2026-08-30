using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class ApprovePathologyReportHandler : IRequestHandler<ApprovePathologyReportCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public ApprovePathologyReportHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
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

            if (report.Status != "TECH_SIGNED")
            {
                throw new ApplicationException("This report must be signed by a technician before it can be approved by a pathologist.");
            }

            if (string.IsNullOrWhiteSpace(request.PathologistRegNo))
            {
                throw new ApplicationException("A pathologist registration number is required to approve this report.");
            }

            // A pathologist sign-off is a medico-legal act, so the approver must be a registered
            // Doctor -- an Admin/LabTechnician-only account has no Doctor record to attribute it to.
            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.UserID == request.LoggedInUserId, cancellationToken);
            if (doctor == null)
            {
                throw new ApplicationException("Only a registered doctor can approve a pathology report as the certifying pathologist.");
            }

            var now = DateTime.UtcNow;

            // 1. Approve the report
            report.Status = "APPROVED";
            report.ApprovedAt = now;
            report.ApprovedByUserId = request.LoggedInUserId;
            report.PathologistDoctorId = doctor.DoctorID;
            report.PathologistName = request.LoggedInUserName;
            report.PathologistRegNo = request.PathologistRegNo.Trim();
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

            // 3. Auto-bill on approval, if the hospital's billing policy is configured for it.
            // Best-effort and never blocks the approval itself -- the report is already signed
            // off medico-legally by this point, so a billing hiccup shouldn't undo that.
            var billingPolicy = await _context.BillingPolicy
                .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
            if (billingPolicy?.LabPathTrigger == "ON_REPORT_APPROVAL")
            {
                await DispatchReportApprovalBillingAsync(report.OrderId, orderLines.Select(l => l.TestId), request, cancellationToken);
            }

            return true;
        }

        private async Task DispatchReportApprovalBillingAsync(
            Guid orderId, IEnumerable<Guid> testIds, ApprovePathologyReportCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var order = await _context.PathologyOrder
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.HospitalId == request.HospitalId, cancellationToken);
                if (order == null) return;

                var billingEncounterId = await PathologyAutoBillingHelper.ResolveBillingEncounterIdAsync(
                    _context, request.HospitalId, order.EncounterId, order.AdmissionId, cancellationToken);
                if (!billingEncounterId.HasValue) return;

                var charges = await PathologyAutoBillingHelper.BuildChargeDetailsAsync(
                    _context, request.HospitalId, testIds, order.OrderId.ToString(), order.OrderedByDoctorId, cancellationToken);
                if (!charges.Any()) return;

                await _mediator.Send(new AddChargeEventRequestModel
                {
                    HospitalId = request.HospitalId,
                    PatientId = order.PatientId,
                    EncounterId = billingEncounterId.Value,
                    Charges = charges,
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName
                }, cancellationToken);
            }
            catch
            {
                // Swallow -- report approval already succeeded and must not be undone by a billing failure.
            }
        }
    }
}
