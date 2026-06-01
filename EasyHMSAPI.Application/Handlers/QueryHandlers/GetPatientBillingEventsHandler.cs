using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Returns EVERY billing visit (Encounter) for a patient — past and present — so the ledger's
    /// Patient &amp; Visits panel always shows the full history, no matter how many bills exist.
    /// A visit with an invoice carries its invoice no / status / amounts; a freshly created visit
    /// with no invoice yet is still returned (status OPEN, amounts derived from its posted charges).
    /// </summary>
    public class GetPatientBillingEventsHandler : IRequestHandler<GetPatientBillingEventsRequestModel, GetPatientBillingEventsResponseModel>
    {
        private const string StatusOpen = "OPEN";
        private readonly AppDbContext _context;

        public GetPatientBillingEventsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientBillingEventsResponseModel> Handle(GetPatientBillingEventsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var encounters = await _context.Encounter
                    .Where(e => e.PatientId == request.PatientId && e.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                if (encounters.Count == 0)
                {
                    return new GetPatientBillingEventsResponseModel
                    {
                        Success = false,
                        Message = $"No encounters found for patient {request.PatientId}."
                    };
                }

                var encounterIds = encounters.Select(e => e.EncounterId).ToList();

                // Latest invoice per encounter (an encounter normally has one).
                var invoices = await _context.BillingInvoice
                    .Where(bi => encounterIds.Contains(bi.EncounterId))
                    .ToListAsync(cancellationToken);
                var invByEncounter = invoices
                    .GroupBy(i => i.EncounterId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First());

                var invoiceIds = invoices.Select(i => i.InvoiceId).ToList();
                var allocByInvoice = (await _context.BillingPaymentAllocation
                        .Where(a => invoiceIds.Contains(a.InvoiceId))
                        .GroupBy(a => a.InvoiceId)
                        .Select(g => new { InvoiceId = g.Key, Sum = g.Sum(x => x.AllocatedAmount) })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.InvoiceId, x => x.Sum);

                // For visits with no invoice yet — derive amounts from posted (non-void) charges + payments.
                var chargeByEncounter = (await _context.BillingChargeEvent
                        .Where(c => encounterIds.Contains(c.EncounterId) && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                        .GroupBy(c => c.EncounterId)
                        .Select(g => new { EncounterId = g.Key, Sum = g.Sum(x => x.NetAmount) })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.EncounterId, x => x.Sum);

                var payments = await _context.BillingPayment
                    .Where(p => encounterIds.Contains(p.EncounterId))
                    .Select(p => new { p.EncounterId, p.PaymentType, p.Amount })
                    .ToListAsync(cancellationToken);
                var payByEncounter = payments
                    .GroupBy(p => p.EncounterId)
                    .ToDictionary(g => g.Key, g => g.Sum(p =>
                        string.Equals(p.PaymentType, BillingConstants.PaymentType.Refund, StringComparison.OrdinalIgnoreCase) ? -p.Amount : p.Amount));

                var doctorIds = encounters.Where(e => e.PrimaryDoctorId.HasValue).Select(e => e.PrimaryDoctorId!.Value).Distinct().ToList();
                var doctorNames = await _context.Doctors
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Join(_context.UserProfiles, d => d.UserID, u => u.UserID, (d, u) => new { d.DoctorID, u.FullName })
                    .ToDictionaryAsync(x => x.DoctorID, x => x.FullName, cancellationToken);

                var encounterInvoiceDetails = new List<PatientEncounterInvoiceDetail>();

                foreach (var e in encounters)
                {
                    invByEncounter.TryGetValue(e.EncounterId, out var inv);

                    decimal totalBilled, received;
                    string? status, invoiceNo, cancelReason, updatedBy;
                    Guid invoiceId;
                    DateTime invoiceDate, updatedAt;
                    bool isCancelled;

                    if (inv != null)
                    {
                        totalBilled = inv.NetAmount ?? 0;
                        received = allocByInvoice.TryGetValue(inv.InvoiceId, out var r) ? r : 0;
                        status = inv.StatusCode;
                        invoiceNo = inv.InvoiceNo;
                        invoiceId = inv.InvoiceId;
                        invoiceDate = inv.InvoiceDate;
                        isCancelled = inv.StatusCode == BillingConstants.InvoiceStatus.Cancelled;
                        cancelReason = isCancelled ? inv.CancelReason : null;
                        updatedAt = inv.UpdatedAt;
                        updatedBy = inv.UpdatedBy;
                    }
                    else
                    {
                        totalBilled = chargeByEncounter.TryGetValue(e.EncounterId, out var cb) ? cb : 0;
                        received = payByEncounter.TryGetValue(e.EncounterId, out var pb) ? pb : 0;
                        status = StatusOpen;
                        invoiceNo = null;
                        invoiceId = Guid.Empty;
                        invoiceDate = e.CreatedAt;
                        isCancelled = false;
                        cancelReason = null;
                        updatedAt = e.UpdatedAt;
                        updatedBy = e.UpdatedBy;
                    }

                    var balance = totalBilled - received;
                    string paymentStatus = totalBilled <= 0 ? "UNPAID" : balance <= 0 ? "PAID" : received > 0 ? "PART" : "UNPAID";
                    var doctorName = e.PrimaryDoctorId.HasValue && doctorNames.TryGetValue(e.PrimaryDoctorId.Value, out var dn) ? dn : null;

                    encounterInvoiceDetails.Add(new PatientEncounterInvoiceDetail
                    {
                        EncounterId = e.EncounterId,
                        InvoiceNo = invoiceNo,
                        InvoiceId = invoiceId,
                        InvoiceDate = invoiceDate,
                        DoctorName = doctorName,
                        Status = status,
                        UpdatedAt = updatedAt,
                        UpdatedBy = updatedBy,
                        IsCancelled = isCancelled,
                        CancelReason = cancelReason,
                        TotalBilled = totalBilled,
                        AmountReceived = received,
                        Balance = balance,
                        PaymentStatus = paymentStatus
                    });
                }

                return new GetPatientBillingEventsResponseModel
                {
                    Success = true,
                    Message = "Patient billing events retrieved successfully.",
                    Data = new GetPatientBillingEventsData
                    {
                        PatientId = request.PatientId,
                        Encounters = encounterInvoiceDetails
                    }
                };
            }
            catch (Exception)
            {
                return new GetPatientBillingEventsResponseModel
                {
                    Success = false,
                    Message = "Error retrieving patient billing events."
                };
            }
        }
    }
}
