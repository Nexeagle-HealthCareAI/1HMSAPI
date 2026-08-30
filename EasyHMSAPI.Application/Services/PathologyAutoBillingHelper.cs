using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    }
}
