using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalBillingDashboardHandler : IRequestHandler<GetHospitalBillingDashboardRequestModel, GetHospitalBillingDashboardResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalBillingDashboardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalBillingDashboardResponseModel> Handle(GetHospitalBillingDashboardRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var encounters = await _context.Encounter
                    .Where(e => e.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                if (encounters.Count == 0)
                {
                    return new GetHospitalBillingDashboardResponseModel
                    {
                        Success = true,
                        Message = "No billing data found for this hospital.",
                        Data = new List<HospitalBillingDashboardData>()
                    };
                }

                var encounterIds = encounters.Select(e => e.EncounterId).ToList();

                var invoices = await _context.BillingInvoice
                    .Where(bi => encounterIds.Contains(bi.EncounterId))
                    .ToListAsync(cancellationToken);

                // Batch-load the lookups so we avoid a per-encounter query (N+1):
                //  - patient names by PatientId
                //  - doctor names by DoctorID (Doctors -> UserProfiles)
                //  - paid totals by InvoiceId (sum of allocations)
                var patientIds = encounters
                    .Select(e => e.PatientId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                var patientNames = (await _context.PatientRegistrations
                        .Where(p => p.PatientId != null && patientIds.Contains(p.PatientId))
                        .Select(p => new { p.PatientId, p.FullName })
                        .ToListAsync(cancellationToken))
                    .GroupBy(x => x.PatientId!)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.FullName).FirstOrDefault());

                var doctorIds = encounters
                    .Where(e => e.PrimaryDoctorId.HasValue)
                    .Select(e => e.PrimaryDoctorId!.Value)
                    .Distinct()
                    .ToList();

                var doctorNames = await _context.Doctors
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Join(_context.UserProfiles, d => d.UserID, u => u.UserID, (d, u) => new { d.DoctorID, u.FullName })
                    .ToDictionaryAsync(x => x.DoctorID, x => x.FullName, cancellationToken);

                // Due/credit must be computed per ENCOUNTER (charges + payments across every
                // invoice that visit has ever had), not per one arbitrary invoice's own
                // allocations — an encounter can fragment across multiple BillingInvoice rows
                // (e.g. day-wise IPD finalize cycles) and money can sit unallocated (a charge-less
                // advance, or money freed up by cancelling a paid charge). Scoping to one invoice
                // under-counts real credit, which is exactly what made the Revenue table's
                // Due/Credit badge unreliable.
                var totalBilledByEncounter = (await _context.BillingChargeEvent
                        .Where(c => encounterIds.Contains(c.EncounterId) && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                        .GroupBy(c => c.EncounterId)
                        .Select(g => new { EncounterId = g.Key, Total = g.Sum(x => x.NetAmount) })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.EncounterId, x => x.Total);

                var paymentTotalsByEncounter = await _context.BillingPayment
                    .Where(p => encounterIds.Contains(p.EncounterId))
                    .GroupBy(p => new { p.EncounterId, p.PaymentType })
                    .Select(g => new { g.Key.EncounterId, g.Key.PaymentType, Total = g.Sum(x => x.Amount) })
                    .ToListAsync(cancellationToken);

                var collectedByEncounter = paymentTotalsByEncounter
                    .Where(x => x.PaymentType == BillingConstants.PaymentType.Payment || x.PaymentType == BillingConstants.PaymentType.Advance)
                    .GroupBy(x => x.EncounterId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

                var refundedByEncounter = paymentTotalsByEncounter
                    .Where(x => x.PaymentType == BillingConstants.PaymentType.Refund)
                    .GroupBy(x => x.EncounterId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

                var invoiceByEncounter = invoices
                    .GroupBy(bi => bi.EncounterId)
                    .ToDictionary(g => g.Key, g => g.First());

                var dashboardData = new List<HospitalBillingDashboardData>();

                foreach (var patientGroup in encounters.GroupBy(e => e.PatientId))
                {
                    var patientId = patientGroup.Key;
                    var encounterDetails = new List<DashboardEncounterDetail>();

                    foreach (var encounter in patientGroup)
                    {
                        if (!invoiceByEncounter.TryGetValue(encounter.EncounterId, out var invoice))
                            continue;

                        var doctorName = encounter.PrimaryDoctorId.HasValue
                            && doctorNames.TryGetValue(encounter.PrimaryDoctorId.Value, out var dn) ? dn : null;

                        bool isCancelled = invoice.StatusCode == BillingConstants.InvoiceStatus.Cancelled;

                        decimal netAmount = isCancelled ? 0 : (totalBilledByEncounter.TryGetValue(encounter.EncounterId, out var billed) ? billed : 0m);
                        decimal totalCollected = isCancelled ? 0 : (collectedByEncounter.TryGetValue(encounter.EncounterId, out var collected) ? collected : 0m);
                        decimal totalRefunded = isCancelled ? 0 : (refundedByEncounter.TryGetValue(encounter.EncounterId, out var refunded) ? refunded : 0m);
                        var totalPaid = totalCollected - totalRefunded;
                        decimal dueAmount = netAmount - totalPaid;

                        encounterDetails.Add(new DashboardEncounterDetail
                        {
                            EncounterId = encounter.EncounterId,
                            VisitType = encounter.EncounterTypeCode,
                            InvoiceNo = invoice.InvoiceNo,
                            InvoiceId = invoice.InvoiceId,
                            InvoiceDate = invoice.InvoiceDate,
                            DoctorName = doctorName,
                            NetAmount = netAmount,
                            DueAmount = dueAmount,
                            PaidAmount = totalPaid,
                            Status = invoice.StatusCode,
                            UpdatedAt = invoice.UpdatedAt,
                            UpdatedBy = invoice.UpdatedBy,
                            IsCancelled = isCancelled,
                            CancelReason = isCancelled ? invoice.CancelReason : null
                        });
                    }

                    if (encounterDetails.Count > 0)
                    {
                        dashboardData.Add(new HospitalBillingDashboardData
                        {
                            PatientId = patientId,
                            PatientName = (patientId != null && patientNames.TryGetValue(patientId, out var name)) ? name : "",
                            Encounters = encounterDetails
                        });
                    }
                }

                return new GetHospitalBillingDashboardResponseModel
                {
                    Success = true,
                    Message = "Hospital billing dashboard data retrieved successfully.",
                    Data = dashboardData
                };
            }
            catch (Exception)
            {
                return new GetHospitalBillingDashboardResponseModel
                {
                    Success = false,
                    Message = "Error retrieving dashboard data."
                };
            }
        }
    }
}
