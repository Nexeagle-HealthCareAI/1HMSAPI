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

            var charges = new List<ChargeDetail>();
            foreach (var test in tests)
            {
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
                        SourceRefId = sourceRefId,
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
    }
}
