using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyPathologyReportHandler : IRequestHandler<VerifyPathologyReportCommand, VerifyPathologyReportResult>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public VerifyPathologyReportHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<VerifyPathologyReportResult> Handle(VerifyPathologyReportCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PathologistName) || string.IsNullOrWhiteSpace(request.PathologistRegNo))
                return new VerifyPathologyReportResult { Success = false, Message = "The verifying pathologist's name and registration number are required." };

            var report = await _context.PathologyReport
                .FirstOrDefaultAsync(r => r.ReportId == request.ReportId && r.OrderId == request.OrderId && r.HospitalId == request.HospitalId, cancellationToken);
            if (report == null)
                return new VerifyPathologyReportResult { Success = false, Message = "Report not found." };

            var order = await _context.PathologyOrder
                .Where(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId)
                .Select(o => o.Status)
                .FirstOrDefaultAsync(cancellationToken);
            if (order == "CANCELLED")
                return new VerifyPathologyReportResult { Success = false, Message = "The order was cancelled." };
            if (report.Status == "VERIFIED")
                return new VerifyPathologyReportResult { Success = false, Message = "This report is already verified." };

            var now = DateTime.UtcNow;
            report.Status = "VERIFIED";
            report.ApprovedAt = now;
            report.ApprovedByUserId = request.LoggedInUserId == Guid.Empty ? null : request.LoggedInUserId;
            report.PathologistName = request.PathologistName.Trim();
            report.PathologistRegNo = request.PathologistRegNo.Trim();
            report.PathologistDoctorId = request.PathologistDoctorId;
            report.UpdatedAt = now;
            report.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            await _context.SaveChangesAsync(cancellationToken);

            // "On report approval" billing: the pathologist's verification IS the approval. Outside the verification's own
            // save so a billing problem never undoes it, but the outcome is reported instead of being swallowed.
            var billingWarning = await BillOnApprovalAsync(report, request, cancellationToken);

            return new VerifyPathologyReportResult
            {
                Success = true,
                Message = billingWarning == null ? "Report verified." : "Report verified. " + billingWarning,
            };
        }

        /// <summary>
        /// Posts the lab charge for the tests on this report when the hospital's lab trigger is ON_REPORT_APPROVAL. Safe to call on every
        /// verification (including a re-verification after an amendment): a test already charged on this order is skipped by
        /// PathologyAutoBillingHelper, so nothing is billed twice. Returns a warning when billing was due but could not be completed.
        /// </summary>
        private async Task<string?> BillOnApprovalAsync(Domain.Entities.PathologyReport report, VerifyPathologyReportCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var policy = await _context.BillingPolicy.FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                if (policy?.LabPathTrigger != "ON_REPORT_APPROVAL") return null;

                var order = await _context.PathologyOrder
                    .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);
                if (order == null) return null;

                var testIds = await _context.PathologyOrderLine
                    .Where(l => l.OrderId == request.OrderId && l.HospitalId == request.HospitalId && l.ReportId == report.ReportId)
                    .Select(l => l.TestId)
                    .ToListAsync(cancellationToken);
                if (testIds.Count == 0) return null;

                var encounterId = await PathologyAutoBillingHelper.ResolveBillingEncounterIdAsync(
                    _context, request.HospitalId, order.EncounterId, order.AdmissionId, cancellationToken);
                if (!encounterId.HasValue)
                    return "The lab charge was not posted because the order has no billing encounter. Add it manually from the Billing tab.";

                var charges = await PathologyAutoBillingHelper.BuildChargeDetailsAsync(
                    _context, request.HospitalId, testIds, order.OrderId.ToString(), order.OrderedByDoctorId, cancellationToken);
                if (charges.Count == 0) return null;   // nothing left to bill (already charged, or no charge linked to the test)

                var warning = await PathologyAutoBillingHelper.PostChargesAndInvoiceAsync(
                    _mediator, request.HospitalId, order.PatientId, encounterId.Value, charges,
                    request.LoggedInUserId == Guid.Empty ? null : request.LoggedInUserId, request.LoggedInUserName, "verified", cancellationToken);
                return warning == null ? null : "Auto-billing: " + warning;
            }
            catch (Exception)
            {
                return "The lab charge could not be posted automatically. Add it manually from the Billing tab.";
            }
        }
    }
}
