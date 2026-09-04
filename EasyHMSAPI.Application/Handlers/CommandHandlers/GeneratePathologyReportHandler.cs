using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Services;
using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Single, freely-repeatable "generate/update report" action, scoped to ONE test line rather
    // than the whole order -- a multi-test order (e.g. CBC + Lipid Profile) gets one independent
    // report per line, each generatable as soon as that one line has a result, instead of waiting
    // for every test in the order to be done. There is no separate technician-sign or
    // pathologist-approve step anymore (SignPathologyReportAsTechnicianHandler and
    // ApprovePathologyReportHandler were removed). Calling this again for a line that already has
    // a report just re-links its current result instead of rejecting with "already exists" --
    // editing a result and clicking "Generate / Update Report" again is the whole workflow.
    public class GeneratePathologyReportHandler : IRequestHandler<GeneratePathologyReportCommand, GeneratePathologyReportResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public GeneratePathologyReportHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<GeneratePathologyReportResponseModel> Handle(GeneratePathologyReportCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Validate the order exists and belongs to this hospital
                var order = await _context.PathologyOrder
                    .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);

                if (order == null)
                {
                    return new GeneratePathologyReportResponseModel { Success = false, Message = "Order not found." };
                }
                // A cancelled order must stay frozen -- otherwise step 5 below flips it back to
                // COMPLETED once every line has a report, silently erasing the cancellation.
                if (order.Status == "CANCELLED")
                {
                    return new GeneratePathologyReportResponseModel { Success = false, Message = "This order was cancelled; reports can no longer be generated for it." };
                }

                // 2. Validate the target line exists on this order and has a result -- only this one
                // line's readiness gates generation now, not its siblings'.
                var line = await _context.PathologyOrderLine
                    .FirstOrDefaultAsync(l => l.OrderLineId == request.OrderLineId && l.OrderId == request.OrderId && l.HospitalId == request.HospitalId, cancellationToken);

                if (line == null)
                {
                    return new GeneratePathologyReportResponseModel { Success = false, Message = "Test line not found on this order." };
                }

                var result = await _context.PathologyResult
                    .FirstOrDefaultAsync(r => r.OrderLineId == line.OrderLineId && r.HospitalId == request.HospitalId, cancellationToken);

                if (result == null)
                {
                    return new GeneratePathologyReportResponseModel
                    {
                        Success = false,
                        Message = "A result has not been entered for this test yet. Please enter the result before generating a report."
                    };
                }

                var now = DateTime.UtcNow;

                // 3. Reuse the existing report if this line already has one -- regenerate in place
                // rather than reject, so a result can be edited and the report re-generated freely.
                var report = line.ReportId.HasValue
                    ? await _context.PathologyReport.FirstOrDefaultAsync(r => r.ReportId == line.ReportId.Value && r.HospitalId == request.HospitalId, cancellationToken)
                    : null;
                var isNewReport = report == null;

                if (report == null)
                {
                    string reportNo = string.Empty;
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                                _context, request.HospitalId, BillingConstants.NumberSeriesCode.LabReport, request.LoggedInUserName, cancellationToken);
                            numberSeries.CurrentValue++;
                            reportNo = NumberSeriesFormatter.Format(
                                numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                            numberSeries.UpdatedAt = now;
                            numberSeries.UpdatedBy = request.LoggedInUserName;
                            break;
                        }
                        catch (DbUpdateException)
                        {
                            _context.ChangeTracker.Clear();
                            if (attempt == 4) throw;
                        }
                    }

                    Guid? newTemplateId = request.TemplateId;
                    if (!newTemplateId.HasValue)
                    {
                        var defaultTemplate = await _context.PathologyReportTemplate
                            .FirstOrDefaultAsync(t => t.HospitalId == request.HospitalId && t.IsDefault && t.IsActive, cancellationToken);
                        newTemplateId = defaultTemplate?.TemplateId;
                    }

                    report = new PathologyReport
                    {
                        ReportId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        OrderId = request.OrderId,
                        TemplateId = newTemplateId,
                        ReportNo = reportNo,
                        Status = "GENERATED",
                        GeneratedAt = now,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName ?? "System",
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName ?? "System"
                    };
                    _context.PathologyReport.Add(report);
                }
                else
                {
                    if (request.TemplateId.HasValue) report.TemplateId = request.TemplateId;
                    report.GeneratedAt = now;
                    report.UpdatedAt = now;
                    report.UpdatedBy = request.LoggedInUserName ?? "System";
                    _context.PathologyReport.Update(report);
                }

                // 4. Link this line's current result to its report -- re-links on every regenerate,
                // so an edited result is picked up next time too.
                result.ReportId = report.ReportId;
                result.UpdatedAt = now;
                result.UpdatedBy = request.LoggedInUserName ?? "System";
                _context.PathologyResult.Update(result);

                line.ReportId = report.ReportId;
                line.UpdatedAt = now;
                line.UpdatedBy = request.LoggedInUserName ?? "System";
                _context.PathologyOrderLine.Update(line);

                // 5. The order is COMPLETED only once every one of its lines has its own report --
                // for a single-test order this is unchanged (one line, one report, done).
                var siblingLines = await _context.PathologyOrderLine
                    .Where(l => l.OrderId == request.OrderId && l.HospitalId == request.HospitalId && l.OrderLineId != line.OrderLineId)
                    .ToListAsync(cancellationToken);
                var allLinesReported = siblingLines.All(l => l.ReportId.HasValue);
                if (allLinesReported && order.Status != "COMPLETED")
                {
                    order.Status = "COMPLETED";
                    order.UpdatedAt = now;
                    order.UpdatedBy = request.LoggedInUserName ?? "System";
                    _context.PathologyOrder.Update(order);
                }

                await _context.SaveChangesAsync(cancellationToken);

                // 6. Auto-bill the first time a report is generated for THIS test, if the hospital's
                // billing policy is configured for it. Deliberately NOT re-dispatched on a
                // regenerate (isNewReport guard) -- AddChargeEventHandler has no dedup for this
                // caller, so firing it again on every "Update Report" click would double-bill the
                // same test. Best-effort, same as CollectPathologySampleHandler's dispatch.
                if (isNewReport)
                {
                    var billingPolicy = await _context.BillingPolicy
                        .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                    if (billingPolicy?.LabPathTrigger == "ON_REPORT_APPROVAL")
                    {
                        await DispatchReportGenerationBillingAsync(order, new[] { line.TestId }, request, cancellationToken);
                    }
                }

                return new GeneratePathologyReportResponseModel
                {
                    Success = true,
                    ReportId = report.ReportId,
                    ReportNo = report.ReportNo
                };
            }
            catch (Exception ex)
            {
                return new GeneratePathologyReportResponseModel { Success = false, Message = ex.Message };
            }
        }

        private async Task DispatchReportGenerationBillingAsync(
            PathologyOrder order, IEnumerable<Guid> testIds, GeneratePathologyReportCommand request, CancellationToken cancellationToken)
        {
            try
            {
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
                // Swallow -- report generation already succeeded and must not be undone by a billing failure.
            }
        }
    }
}
