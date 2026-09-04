using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Full order editing -- unlike CancelPathologyOrderHandler (whole-order only, blocked once any
    // report exists), this allows changing the patient and adding/removing tests at ANY point in the
    // order's progress, including after a sample is collected or a report is generated. That's a
    // deliberate product decision (confirmed via clarifying question), so every downstream effect of
    // "the tests or the patient changed" has to be reconciled here rather than gated away:
    //  - Removing a line that already has a report deletes that report (and its result) -- there is
    //    no partial-undo of a generated report, only whole deletion (frontend warns before submitting
    //    a removal that would hit one).
    //  - Reassigning the patient invalidates (deletes) any surviving line's already-generated report,
    //    since its PDF has the OLD patient's name baked in at render time -- GeneratePathologyReport
    //    can freely re-render it under the corrected patient at any time.
    //  - Billing is reconciled per-line (void a removed test's own charge) or, when the billing
    //    context itself changed (different patient and/or different encounter/admission), by voiding
    //    everything already posted for this order and rebilling the surviving + newly-added tests
    //    against the new context -- mirrors CreatePathologyOrderHandler's own dispatch shape.
    public class UpdatePathologyOrderHandler : IRequestHandler<UpdatePathologyOrderCommand, UpdatePathologyOrderResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public UpdatePathologyOrderHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<UpdatePathologyOrderResponseModel> Handle(UpdatePathologyOrderCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var order = await _context.PathologyOrder
                    .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);
                if (order == null)
                {
                    return new UpdatePathologyOrderResponseModel { Success = false, Message = "Order not found." };
                }
                if (order.Status == "CANCELLED")
                {
                    return new UpdatePathologyOrderResponseModel { Success = false, Message = "Cannot edit a cancelled order." };
                }

                var distinctTestIds = request.TestIds.Distinct().ToList();
                var ownedTestCount = await _context.PathologyTestMaster
                    .CountAsync(t => distinctTestIds.Contains(t.TestId) && t.HospitalId == request.HospitalId, cancellationToken);
                if (ownedTestCount != distinctTestIds.Count)
                {
                    return new UpdatePathologyOrderResponseModel { Success = false, Message = "One or more selected tests are not in this hospital's catalog." };
                }

                var existingLines = await _context.PathologyOrderLine
                    .Where(l => l.OrderId == order.OrderId && l.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);
                var existingTestIds = existingLines.Select(l => l.TestId).ToHashSet();
                var newTestIdSet = distinctTestIds.ToHashSet();
                var addedTestIds = newTestIdSet.Except(existingTestIds).ToList();
                var removedLines = existingLines.Where(l => !newTestIdSet.Contains(l.TestId)).ToList();

                var involvedTestIds = existingTestIds.Union(newTestIdSet).ToList();
                var chargeIdByTest = await _context.PathologyTestMaster
                    .Where(t => involvedTestIds.Contains(t.TestId) && t.HospitalId == request.HospitalId)
                    .ToDictionaryAsync(t => t.TestId, t => t.ChargeId, cancellationToken);

                var billingPolicy = await _context.BillingPolicy.FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                bool autoBillOnOrder = billingPolicy?.LabPathTrigger == "ON_ORDER";

                var now = DateTime.UtcNow;
                var actor = request.LoggedInUserName ?? request.LoggedInUserId.ToString();

                bool patientChanged = order.PatientId != request.PatientId;
                bool billingContextChanged = patientChanged || order.EncounterId != request.EncounterId || order.AdmissionId != request.AdmissionId;

                // --- Remove lines no longer selected ---
                foreach (var line in removedLines)
                {
                    if (line.ReportId.HasValue)
                    {
                        var report = await _context.PathologyReport
                            .FirstOrDefaultAsync(r => r.ReportId == line.ReportId.Value && r.HospitalId == request.HospitalId, cancellationToken);
                        if (report != null) _context.PathologyReport.Remove(report);
                    }
                    var result = await _context.PathologyResult
                        .FirstOrDefaultAsync(r => r.OrderLineId == line.OrderLineId && r.HospitalId == request.HospitalId, cancellationToken);
                    if (result != null) _context.PathologyResult.Remove(result);

                    if (chargeIdByTest.TryGetValue(line.TestId, out var removedChargeId) && removedChargeId.HasValue)
                    {
                        var chargesToVoid = await _context.BillingChargeEvent
                            .Where(c => c.SourceModule == BillingConstants.SourceModule.LabPath
                                && c.SourceRefId == order.OrderId.ToString()
                                && c.ChargeId == removedChargeId.Value
                                && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                            .ToListAsync(cancellationToken);
                        foreach (var charge in chargesToVoid)
                        {
                            VoidCharge(charge, now, actor, "Test removed from pathology order");
                        }
                    }

                    _context.PathologyOrderLine.Remove(line);
                }

                // --- Patient reassignment: stale (already-rendered) reports on surviving lines no
                // longer reflect the corrected patient, so they're invalidated rather than left
                // pointing at a PDF with the wrong name. The entered results themselves are kept --
                // GeneratePathologyReportHandler can freely re-render from them at any time.
                if (patientChanged)
                {
                    var survivingLinesWithReport = existingLines
                        .Where(l => newTestIdSet.Contains(l.TestId) && l.ReportId.HasValue)
                        .ToList();
                    foreach (var line in survivingLinesWithReport)
                    {
                        var report = await _context.PathologyReport
                            .FirstOrDefaultAsync(r => r.ReportId == line.ReportId!.Value && r.HospitalId == request.HospitalId, cancellationToken);
                        if (report != null) _context.PathologyReport.Remove(report);
                        line.ReportId = null;
                        line.UpdatedAt = now;
                        line.UpdatedBy = actor;
                        _context.PathologyOrderLine.Update(line);
                    }
                    order.PatientId = request.PatientId;
                }

                // --- Billing context changed (new patient and/or new encounter/admission): void
                // everything already posted for this order so nothing keeps billing against a stale
                // patient/visit; surviving + newly-added tests get rebilled below against the new
                // context instead.
                if (billingContextChanged)
                {
                    var chargesToVoid = await _context.BillingChargeEvent
                        .Where(c => c.SourceModule == BillingConstants.SourceModule.LabPath
                            && c.SourceRefId == order.OrderId.ToString()
                            && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                        .ToListAsync(cancellationToken);
                    foreach (var charge in chargesToVoid)
                    {
                        VoidCharge(charge, now, actor, patientChanged
                            ? "Pathology order reassigned to a different patient"
                            : "Pathology order's billing context changed");
                    }
                }

                order.EncounterId = request.EncounterId;
                order.AdmissionId = request.AdmissionId;
                order.SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? order.SourceType : request.SourceType;
                order.Notes = request.Notes;
                order.IsStat = request.IsStat;
                order.UpdatedAt = now;
                order.UpdatedBy = actor;
                _context.PathologyOrder.Update(order);

                var newLines = addedTestIds.Select(testId => new PathologyOrderLine
                {
                    HospitalId = request.HospitalId,
                    OrderId = order.OrderId,
                    TestId = testId,
                    Status = "PENDING",
                    CreatedBy = actor,
                }).ToList();
                _context.PathologyOrderLine.AddRange(newLines);

                await _context.SaveChangesAsync(cancellationToken);

                string? billingWarning = null;
                if (autoBillOnOrder)
                {
                    var billingEncounterId = await PathologyAutoBillingHelper.ResolveBillingEncounterIdAsync(
                        _context, request.HospitalId, order.EncounterId, order.AdmissionId, cancellationToken);

                    if (billingEncounterId.HasValue)
                    {
                        // Newly-added tests always need a fresh charge. When the billing context also
                        // changed, the surviving pre-existing tests were just voided above and need
                        // rebilling too -- combine both sets in one dispatch (Distinct guards the rare
                        // case a test appears in both, e.g. removed then re-added in the same edit).
                        var testIdsToBill = billingContextChanged
                            ? newTestIdSet.ToList()
                            : addedTestIds;

                        if (testIdsToBill.Count > 0)
                        {
                            var charges = await PathologyAutoBillingHelper.BuildChargeDetailsAsync(
                                _context, request.HospitalId, testIdsToBill, order.OrderId.ToString(), order.OrderedByDoctorId, cancellationToken);
                            if (charges.Any())
                            {
                                billingWarning = await PathologyAutoBillingHelper.PostChargesAndInvoiceAsync(
                                    _mediator, request.HospitalId, order.PatientId, billingEncounterId.Value, charges,
                                    request.LoggedInUserId, actor, "updated", cancellationToken);
                            }
                        }
                    }
                    else if (addedTestIds.Count > 0 || billingContextChanged)
                    {
                        billingWarning = "Order updated, but auto-billing was skipped: no open visit/encounter to bill against. " +
                            "Add the charge manually from the Billing tab.";
                    }
                }

                return new UpdatePathologyOrderResponseModel { Success = true, BillingWarning = billingWarning };
            }
            catch (Exception ex)
            {
                return new UpdatePathologyOrderResponseModel { Success = false, Message = ex.Message };
            }
        }

        private static void VoidCharge(BillingChargeEvent charge, DateTime now, string actor, string reason)
        {
            charge.StatusCode = BillingConstants.ChargeEventStatus.Void;
            charge.VoidedAt = now;
            charge.VoidedBy = actor;
            charge.VoidReason = reason;
            charge.UpdatedAt = now;
            charge.UpdatedBy = actor;
        }
    }
}
