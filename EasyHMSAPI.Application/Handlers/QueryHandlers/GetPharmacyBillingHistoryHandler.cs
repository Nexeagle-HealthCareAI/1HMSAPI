using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Raw per-invoice pharmacy sales register — one row per checkout invoice (both counter and
    // IPD-posted pharmacy sales), so a pharmacist/manager can reconcile "everything billed today"
    // rather than only seeing the aggregated analytics (PharmacyAnalyticsQueryHandlers). Built from
    // BillingChargeEvent (SourceModule-scoped) joined to BillingInvoice via BillingInvoiceChargeEvent
    // — same join shape as GetReturnableInvoiceLinesHandler's soft ChargeId join, but grouped up to
    // invoice level instead of down to line level.
    public class GetPharmacyBillingHistoryHandler : IRequestHandler<GetPharmacyBillingHistoryRequestModel, GetPharmacyBillingHistoryResponseModel>
    {
        private static readonly string[] PharmacySourceModules =
        {
            BillingConstants.SourceModule.PharmacyCounter,
            BillingConstants.SourceModule.PharmacyIpd,
        };

        private readonly AppDbContext _context;

        public GetPharmacyBillingHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPharmacyBillingHistoryResponseModel> Handle(GetPharmacyBillingHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var chargeQuery = _context.BillingChargeEvent.AsNoTracking().Where(c =>
                c.HospitalId == request.HospitalId
                && c.SourceModule != null && PharmacySourceModules.Contains(c.SourceModule)
                && c.StatusCode != BillingConstants.ChargeEventStatus.Void);

            if (request.FromDate.HasValue)
                chargeQuery = chargeQuery.Where(c => c.ServiceDate >= request.FromDate.Value.Date);
            if (request.ToDate.HasValue)
                chargeQuery = chargeQuery.Where(c => c.ServiceDate < request.ToDate.Value.Date.AddDays(1));

            var charges = await chargeQuery
                .Select(c => new { c.ChargeEventId, c.EncounterId, c.SourceModule, c.Qty, c.NetAmount })
                .ToListAsync(cancellationToken);

            if (charges.Count == 0)
                return new GetPharmacyBillingHistoryResponseModel();

            var chargeEventIds = charges.Select(c => c.ChargeEventId).ToList();
            var invoiceLinks = await _context.BillingInvoiceChargeEvent.AsNoTracking()
                .Where(l => chargeEventIds.Contains(l.ChargeEventId))
                .ToListAsync(cancellationToken);

            var invoiceIdByChargeEventId = invoiceLinks
                .GroupBy(l => l.ChargeEventId)
                .ToDictionary(g => g.Key, g => g.First().InvoiceId);

            var invoiceIds = invoiceLinks.Select(l => l.InvoiceId).Distinct().ToList();
            var invoices = await _context.BillingInvoice.AsNoTracking()
                .Where(i => invoiceIds.Contains(i.InvoiceId))
                .ToDictionaryAsync(i => i.InvoiceId, cancellationToken);

            var encounterIds = invoices.Values.Select(i => i.EncounterId).Distinct().ToList();
            var payments = await _context.BillingPayment.AsNoTracking()
                .Where(p => encounterIds.Contains(p.EncounterId) && p.PaymentType == BillingConstants.PaymentType.Payment)
                .OrderByDescending(p => p.PaidAt)
                .ToListAsync(cancellationToken);
            var paymentModeByEncounterId = payments
                .GroupBy(p => p.EncounterId)
                .ToDictionary(g => g.Key, g => g.First().PaymentMode);

            // Returns are never applied to the original BillingChargeEvent/BillingInvoice rows (see
            // CreatePharmacyReturnHandler's own comment on why -- no partial-qty adjustment
            // primitive exists yet), so without this a returned sale still shows its full original
            // NetAmount here with no sign anything was ever refunded -- net sales overstated by
            // every processed return.
            var returnsByInvoiceId = await _context.PharmacyReturn.AsNoTracking()
                .Where(r => invoiceIds.Contains(r.InvoiceId))
                .GroupBy(r => r.InvoiceId)
                .Select(g => new { InvoiceId = g.Key, TotalRefund = g.Sum(r => r.TotalRefundAmount) })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.TotalRefund, cancellationToken);

            var patientIds = invoices.Values.Where(i => i.PatientId != null).Select(i => i.PatientId!).Distinct().ToList();
            var patientNames = await _context.PatientRegistrations.AsNoTracking()
                .Where(p => patientIds.Contains(p.PatientId))
                .Select(p => new { p.PatientId, p.FullName })
                .Distinct()
                .ToDictionaryAsync(p => p.PatientId, p => p.FullName, cancellationToken);

            var grouped = charges
                .Where(c => invoiceIdByChargeEventId.ContainsKey(c.ChargeEventId))
                .GroupBy(c => invoiceIdByChargeEventId[c.ChargeEventId])
                .Select(g =>
                {
                    var invoice = invoices[g.Key];
                    return new PharmacyBillRow
                    {
                        InvoiceId = invoice.InvoiceId,
                        InvoiceNo = invoice.InvoiceNo,
                        InvoiceDate = invoice.InvoiceDate,
                        PatientId = invoice.PatientId,
                        PatientName = invoice.PatientId != null && patientNames.TryGetValue(invoice.PatientId, out var n) ? n : null,
                        SourceModule = g.First().SourceModule!,
                        ItemCount = g.Count(),
                        TotalQty = g.Sum(c => c.Qty),
                        NetAmount = g.Sum(c => c.NetAmount),
                        ReturnedAmount = returnsByInvoiceId.TryGetValue(invoice.InvoiceId, out var refunded) ? refunded : 0m,
                        PaymentMode = paymentModeByEncounterId.TryGetValue(invoice.EncounterId, out var mode) ? mode : null,
                        ProcessedBy = invoice.CreatedBy,
                        StatusCode = invoice.StatusCode,
                    };
                })
                .OrderByDescending(b => b.InvoiceDate)
                .ToList();

            var totalAmount = grouped.Sum(b => b.NetAmount);
            var totalReturnedAmount = grouped.Sum(b => b.ReturnedAmount);
            return new GetPharmacyBillingHistoryResponseModel
            {
                Bills = grouped,
                TotalAmount = totalAmount,
                TotalReturnedAmount = totalReturnedAmount,
                NetSalesAmount = totalAmount - totalReturnedAmount,
                TotalBills = grouped.Count,
            };
        }
    }
}
