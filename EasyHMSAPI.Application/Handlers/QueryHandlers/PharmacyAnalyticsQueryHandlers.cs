using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Pure aggregations over BillingChargeEvent, scoped to pharmacy's two SourceModule values and
    // excluding voided lines — no new schema needed, GST/HSN fields already live on the charge
    // event. Ranges are pulled into memory before grouping since week/month bucketing and the ABC
    // running-cumulative-percent pass aren't naturally expressible as one SQL translation, and a
    // pharmacy's charge-event volume for a reporting window is small enough for this to be fine.
    public class PharmacyAnalyticsQueryHandlers :
        IRequestHandler<GetPharmacySalesTrendRequestModel, GetPharmacySalesTrendResponseModel>,
        IRequestHandler<GetPharmacyAbcAnalysisRequestModel, GetPharmacyAbcAnalysisResponseModel>,
        IRequestHandler<GetPharmacyGstLiabilityRequestModel, GetPharmacyGstLiabilityResponseModel>,
        IRequestHandler<GetPharmacyExpiryLossPreventedRequestModel, GetPharmacyExpiryLossPreventedResponseModel>
    {
        private static readonly string[] PharmacySourceModules =
        {
            BillingConstants.SourceModule.PharmacyCounter,
            BillingConstants.SourceModule.PharmacyIpd,
        };

        private readonly AppDbContext _context;

        public PharmacyAnalyticsQueryHandlers(AppDbContext context)
        {
            _context = context;
        }

        private IQueryable<Domain.Entities.BillingChargeEvent> PharmacyChargesInRange(Guid hospitalId, DateTime fromDate, DateTime toDate)
        {
            var toExclusive = toDate.Date.AddDays(1);
            return _context.BillingChargeEvent.AsNoTracking().Where(c =>
                c.HospitalId == hospitalId
                && c.SourceModule != null && PharmacySourceModules.Contains(c.SourceModule)
                && c.StatusCode != BillingConstants.ChargeEventStatus.Void
                && c.ServiceDate >= fromDate.Date && c.ServiceDate < toExclusive);
        }

        public async Task<GetPharmacySalesTrendResponseModel> Handle(GetPharmacySalesTrendRequestModel request, CancellationToken cancellationToken)
        {
            var charges = await PharmacyChargesInRange(request.HospitalId, request.FromDate, request.ToDate)
                .Select(c => new { c.ServiceDate, c.NetAmount, c.Qty })
                .ToListAsync(cancellationToken);

            var groupBy = (request.GroupBy ?? "DAY").Trim().ToUpperInvariant();
            Func<DateTime, DateTime> periodStart = groupBy switch
            {
                "MONTH" => d => new DateTime(d.Year, d.Month, 1),
                "WEEK" => d => d.Date.AddDays(-(int)d.DayOfWeek),
                _ => d => d.Date,
            };
            Func<DateTime, string> periodLabel = groupBy switch
            {
                "MONTH" => d => d.ToString("MMM yyyy"),
                "WEEK" => d => $"Week of {d:dd MMM yyyy}",
                _ => d => d.ToString("dd MMM yyyy"),
            };

            var points = charges
                .GroupBy(c => periodStart(c.ServiceDate.Date))
                .Select(g => new SalesTrendPoint
                {
                    PeriodStart = g.Key,
                    PeriodLabel = periodLabel(g.Key),
                    TotalSales = g.Sum(x => x.NetAmount),
                    TotalQty = g.Sum(x => x.Qty),
                    LineCount = g.Count(),
                })
                .OrderBy(p => p.PeriodStart)
                .ToList();

            return new GetPharmacySalesTrendResponseModel { Points = points };
        }

        public async Task<GetPharmacyAbcAnalysisResponseModel> Handle(GetPharmacyAbcAnalysisRequestModel request, CancellationToken cancellationToken)
        {
            var charges = await PharmacyChargesInRange(request.HospitalId, request.FromDate, request.ToDate)
                .Select(c => new { c.ChargeId, c.DisplayName, c.NetAmount, c.Qty })
                .ToListAsync(cancellationToken);

            var chargeIds = charges.Where(c => c.ChargeId.HasValue).Select(c => c.ChargeId!.Value).Distinct().ToList();
            var itemNames = await _context.InventoryItem.AsNoTracking()
                .Where(i => i.HospitalId == request.HospitalId && i.ChargeId.HasValue && chargeIds.Contains(i.ChargeId.Value))
                .ToDictionaryAsync(i => i.ChargeId!.Value, i => new { i.InventoryItemId, i.ItemName }, cancellationToken);

            var grouped = charges
                .GroupBy(c => c.ChargeId)
                .Select(g =>
                {
                    var itemInfo = g.Key.HasValue && itemNames.TryGetValue(g.Key.Value, out var info) ? info : null;
                    return new AbcAnalysisRow
                    {
                        InventoryItemId = itemInfo?.InventoryItemId,
                        ItemName = itemInfo?.ItemName ?? g.First().DisplayName ?? "Unknown",
                        TotalValue = g.Sum(x => x.NetAmount),
                        TotalQty = g.Sum(x => x.Qty),
                    };
                })
                .OrderByDescending(r => r.TotalValue)
                .ToList();

            var grandTotal = grouped.Sum(r => r.TotalValue);
            decimal running = 0;
            foreach (var row in grouped)
            {
                running += row.TotalValue;
                row.CumulativePercent = grandTotal > 0 ? Math.Round(running / grandTotal * 100, 2) : 0;
                row.Class = row.CumulativePercent <= 70 ? "A" : row.CumulativePercent <= 90 ? "B" : "C";
            }

            return new GetPharmacyAbcAnalysisResponseModel { Items = grouped };
        }

        public async Task<GetPharmacyGstLiabilityResponseModel> Handle(GetPharmacyGstLiabilityRequestModel request, CancellationToken cancellationToken)
        {
            var charges = await PharmacyChargesInRange(request.HospitalId, request.FromDate, request.ToDate)
                .Select(c => new { c.HsnSacCode, c.GstRate, c.TaxableAmount, c.CgstAmount, c.SgstAmount, c.IgstAmount, c.TaxAmount, c.NetAmount })
                .ToListAsync(cancellationToken);

            var rows = charges
                .GroupBy(c => new { c.HsnSacCode, c.GstRate })
                .Select(g => new GstLiabilityRow
                {
                    HsnSacCode = g.Key.HsnSacCode,
                    GstRate = g.Key.GstRate,
                    TaxableAmount = g.Sum(x => x.TaxableAmount ?? 0),
                    CgstAmount = g.Sum(x => x.CgstAmount),
                    SgstAmount = g.Sum(x => x.SgstAmount),
                    IgstAmount = g.Sum(x => x.IgstAmount),
                    TotalTax = g.Sum(x => x.TaxAmount),
                    TotalSales = g.Sum(x => x.NetAmount),
                })
                .OrderByDescending(r => r.TotalTax)
                .ToList();

            return new GetPharmacyGstLiabilityResponseModel { Rows = rows, GrandTotalTax = rows.Sum(r => r.TotalTax) };
        }

        public async Task<GetPharmacyExpiryLossPreventedResponseModel> Handle(GetPharmacyExpiryLossPreventedRequestModel request, CancellationToken cancellationToken)
        {
            var toExclusive = request.ToDate.Date.AddDays(1);
            var recoveredValue = await _context.VendorReturnNote.AsNoTracking()
                .Where(n => n.HospitalId == request.HospitalId && n.GeneratedAt >= request.FromDate.Date && n.GeneratedAt < toExclusive)
                .SumAsync(n => (decimal?)n.TotalValue, cancellationToken) ?? 0m;
            var rtvNoteCount = await _context.VendorReturnNote.AsNoTracking()
                .CountAsync(n => n.HospitalId == request.HospitalId && n.GeneratedAt >= request.FromDate.Date && n.GeneratedAt < toExclusive, cancellationToken);

            var today = DateTime.UtcNow.Date;
            var atRiskBatches = await _context.Batch.AsNoTracking()
                .Where(b => b.HospitalId == request.HospitalId && b.Status == "ACTIVE" && b.RemainingQty > 0 && b.ExpiryDate != null)
                .Select(b => new { b.ExpiryDate, b.RemainingQty, b.UnitCost })
                .ToListAsync(cancellationToken);

            var atRisk = atRiskBatches
                .Where(b => ExpiryBucketCalculator.Compute(b.ExpiryDate, today) is ExpiryBucketCalculator.Orange or ExpiryBucketCalculator.Red)
                .ToList();

            return new GetPharmacyExpiryLossPreventedResponseModel
            {
                RecoveredValue = recoveredValue,
                AtRiskValue = atRisk.Sum(b => (b.UnitCost ?? 0) * b.RemainingQty),
                AtRiskBatchCount = atRisk.Count,
                RtvNoteCount = rtvNoteCount,
            };
        }
    }
}
