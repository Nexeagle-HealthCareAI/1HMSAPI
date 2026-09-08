using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    // Shared by every pathology auto-billing trigger point (order creation, report approval, ...)
    // so each one resolves "which encounter does this bill against" and "what does each linked
    // test cost" the same way, instead of copy-pasting the ChargeMaster lookup per trigger.
    public static class PathologyAutoBillingHelper
    {
        // An IPD order carries AdmissionId, not EncounterId -- ClinicalOrderCommandHandlers already
        // resolves the admission's own Encounter (Admission.EncounterId) for this exact reason, so
        // this mirrors that instead of inventing a second way to find "the encounter for this stay."
        public static async Task<Guid?> ResolveBillingEncounterIdAsync(
            AppDbContext context, Guid hospitalId, Guid? encounterId, Guid? admissionId, CancellationToken cancellationToken)
        {
            if (encounterId.HasValue)
                return encounterId;

            if (!admissionId.HasValue)
                return null;

            var admission = await context.Admission
                .FirstOrDefaultAsync(a => a.AdmissionId == admissionId.Value && a.HospitalId == hospitalId, cancellationToken);
            return admission?.EncounterId;
        }

        // sourceRefId is the order's own id (as a string). Each posted charge gets a
        // "{orderId}:{testId}" SourceRefId rather than the bare order id, so a later void-by-line
        // (UpdatePathologyOrderHandler removing one test) can target exactly that test's charge --
        // without this, two tests sharing the same ChargeId (nothing prevents that in the catalog)
        // would have their charges indistinguishable from each other, and removing one test could
        // silently void the other's still-owed charge too.
        public static async Task<List<ChargeDetail>> BuildChargeDetailsAsync(
            AppDbContext context, Guid hospitalId, IEnumerable<Guid> testIds,
            string sourceRefId, Guid? attributedDoctorId, CancellationToken cancellationToken)
        {
            var tests = await context.PathologyTestMaster
                .Where(t => testIds.Contains(t.TestId) && t.HospitalId == hospitalId)
                .ToListAsync(cancellationToken);

            var chargeIds = tests.Where(t => t.ChargeId.HasValue).Select(t => t.ChargeId!.Value).Distinct().ToList();
            var chargeMasters = chargeIds.Count == 0
                ? new Dictionary<Guid, ChargeMaster>()
                : await context.ChargeMaster
                    .Where(c => c.HospitalId == hospitalId && chargeIds.Contains(c.ChargeId))
                    .ToDictionaryAsync(c => c.ChargeId, cancellationToken);

            // Guards against double-billing the same test on this order: the three call sites
            // (order creation, sample collection, report generation) each independently read
            // BillingPolicy.LabPathTrigger at their own point in the lifecycle, so if that policy
            // value changes while an order is mid-flight (or a caller is invoked twice for any
            // other reason), a test already charged here would otherwise be charged again with no
            // check anywhere for "was this order+test already billed." Skips any test that already
            // has a non-voided charge under its "{sourceRefId}:{testId}" SourceRefId.
            var candidateRefIds = tests.Select(t => $"{sourceRefId}:{t.TestId}").ToList();
            var alreadyChargedRefIds = candidateRefIds.Count == 0
                ? new HashSet<string>()
                : (await context.BillingChargeEvent
                    .Where(c => c.HospitalId == hospitalId && c.VoidedAt == null && c.SourceRefId != null && candidateRefIds.Contains(c.SourceRefId))
                    .Select(c => c.SourceRefId!)
                    .ToListAsync(cancellationToken))
                  .ToHashSet();

            var charges = new List<ChargeDetail>();
            foreach (var test in tests)
            {
                if (alreadyChargedRefIds.Contains($"{sourceRefId}:{test.TestId}"))
                    continue;

                if (test.ChargeId.HasValue && chargeMasters.TryGetValue(test.ChargeId.Value, out var master))
                {
                    charges.Add(new ChargeDetail
                    {
                        ChargeId = test.ChargeId.Value,
                        // AddChargeEventHandler writes this straight onto BillingChargeEvent with no
                        // fallback to the ChargeMaster's own name -- omitting it made every auto-bill
                        // call here fail silently (a real, live-confirmed bug: the charge post
                        // returned success:false, but CreatePathologyOrderHandler/CollectPathologySample
                        // Handler/ApprovePathologyReportHandler never check that response, so the
                        // order/collection/approval itself always reported success anyway).
                        DisplayName = master.DisplayName ?? test.TestName,
                        Qty = 1,
                        Rate = master.DefaultRate,
                        CategoryCode = "LAB_PATH",
                        SourceModule = BillingConstants.SourceModule.LabPath,
                        SourceRefId = $"{sourceRefId}:{test.TestId}",
                        AttributedDoctorId = attributedDoctorId
                    });
                }
            }
            return charges;
        }

        // Posts the charges and, when that succeeds, immediately creates/links a draft invoice for
        // the encounter -- without this, a charge could sit posted-but-uninvoiced indefinitely (the
        // hospital-wide Billing Dashboard and Pathology's own Billing tab both only show encounters
        // that already have a BillingInvoice row, so an uninvoiced encounter is invisible on either
        // screen even though the money was really billed -- a real, live-confirmed gap). Returns a
        // warning string when the charge post or the draft-invoice creation didn't fully succeed;
        // null when everything went through cleanly.
        public static async Task<string?> PostChargesAndInvoiceAsync(
            IMediator mediator, Guid hospitalId, string? patientId, Guid encounterId, List<ChargeDetail> charges,
            Guid? loggedInUserId, string? loggedInUserName, string successVerb, CancellationToken cancellationToken)
        {
            var chargeResponse = await mediator.Send(new AddChargeEventRequestModel
            {
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterId = encounterId,
                Charges = charges,
                LoggedInUserId = loggedInUserId,
                LoggedInUserName = loggedInUserName
            }, cancellationToken);

            if (chargeResponse.Success != true)
            {
                return $"Order {successVerb}, but auto-billing failed: {chargeResponse.Message} " +
                    "Add the charge manually from the Billing tab.";
            }

            var invoiceResponse = await mediator.Send(new CreateDraftInvoiceRequestModel
            {
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterId = encounterId,
                LoggedInUserId = loggedInUserId,
                LoggedInUserName = loggedInUserName
            }, cancellationToken);

            if (invoiceResponse.Success != true)
            {
                return $"Order {successVerb} and billed, but the invoice could not be created automatically: {invoiceResponse.Message} " +
                    "Create it manually from the Billing tab so it shows up on the dashboard.";
            }

            return null;
        }

        // The exact SourceRefId a charge for this order+test was posted under (see the comment on
        // BuildChargeDetailsAsync above). NOT usable inside an EF LINQ Where() -- EF can't translate
        // a call to this method to SQL, so callers building an EF query must inline the same
        // "{orderId}:{testId}" string themselves (see UpdatePathologyOrderHandler).
        public static string LineSourceRefId(Guid orderId, Guid testId) => $"{orderId}:{testId}";
    }
}
