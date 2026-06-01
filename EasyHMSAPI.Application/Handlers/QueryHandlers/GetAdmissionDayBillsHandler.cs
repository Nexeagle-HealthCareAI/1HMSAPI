using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Day-wise view of a billing visit (Encounter) — opt-in, no admission required.
    /// Billing days are 24h windows anchored to the visit's start (the earliest charge's
    /// service date). Closed days return their frozen snapshot; the open day and any
    /// not-yet-closed days are computed live from the un-billed posted charges, with late
    /// charges rolling forward into the first open day.
    /// </summary>
    public class GetAdmissionDayBillsHandler : IRequestHandler<GetAdmissionDayBillsRequestModel, GetAdmissionDayBillsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionDayBillsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionDayBillsResponseModel> Handle(GetAdmissionDayBillsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                    return new GetAdmissionDayBillsResponseModel { Success = false, Message = "HospitalId and EncounterId are required." };

                var charges = await _context.BillingChargeEvent
                    .Where(c => c.EncounterId == request.EncounterId && c.HospitalId == request.HospitalId
                                && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                    .ToListAsync(cancellationToken);

                var now = DateTime.UtcNow;
                // Anchor Day 1 on the earliest charge's service date; fall back to now when empty.
                var anchor = charges.Count > 0
                    ? DateTime.SpecifyKind(charges.Min(c => c.ServiceDate), DateTimeKind.Utc)
                    : now;

                var closedBills = await _context.AdmissionDayBill
                    .Where(b => b.EncounterId == request.EncounterId && b.HospitalId == request.HospitalId
                                && b.StatusCode == BillingConstants.DayBillStatus.Closed)
                    .OrderBy(b => b.DayNumber)
                    .ToListAsync(cancellationToken);

                var closedIds = closedBills.Select(b => b.AdmissionDayBillId).ToList();
                var closedLines = closedIds.Count == 0
                    ? new List<AdmissionDayBillLine>()
                    : await _context.AdmissionDayBillLine
                        .Where(l => closedIds.Contains(l.AdmissionDayBillId))
                        .ToListAsync(cancellationToken);
                var billedChargeIds = closedLines.Select(l => l.ChargeEventId).ToHashSet();

                var payments = await _context.BillingPayment
                    .Where(p => p.EncounterId == request.EncounterId && p.HospitalId == request.HospitalId)
                    .Select(p => new { p.PaymentType, p.Amount })
                    .ToListAsync(cancellationToken);
                var received = payments.Sum(p => IsRefund(p.PaymentType) ? -p.Amount : p.Amount);

                var totalCharged = charges.Sum(c => c.NetAmount);
                var patientId = charges.FirstOrDefault()?.PatientId;

                var maxClosedDay = closedBills.Count > 0 ? closedBills.Max(b => b.DayNumber) : 0;
                var firstOpenDay = maxClosedDay + 1;

                var liveByDay = new Dictionary<int, List<BillingChargeEvent>>();
                foreach (var c in charges.Where(c => !billedChargeIds.Contains(c.ChargeEventId)))
                {
                    var natural = DayIndexOf(c.ServiceDate, anchor);
                    var assigned = Math.Max(natural, firstOpenDay);
                    if (!liveByDay.TryGetValue(assigned, out var list)) { list = new List<BillingChargeEvent>(); liveByDay[assigned] = list; }
                    list.Add(c);
                }

                var elapsedDays = DayIndexOf(now, anchor);
                var totalDays = Math.Max(Math.Max(elapsedDays, maxClosedDay), liveByDay.Count > 0 ? liveByDay.Keys.Max() : 0);
                if (totalDays < 1) totalDays = 1;

                var closedByDay = closedBills.ToDictionary(b => b.DayNumber);
                var closedLinesByBill = closedLines.GroupBy(l => l.AdmissionDayBillId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var days = new List<AdmissionDayView>();
                decimal cumulative = 0;
                for (var d = 1; d <= totalDays; d++)
                {
                    var from = anchor.AddDays(d - 1);
                    var to = from.AddDays(1);
                    var view = new AdmissionDayView
                    {
                        DayNumber = d,
                        FromUtc = from,
                        ToUtc = to,
                        IsCurrent = now >= from && now < to,
                    };

                    if (closedByDay.TryGetValue(d, out var cb))
                    {
                        view.IsClosed = true;
                        view.AdmissionDayBillId = cb.AdmissionDayBillId;
                        view.InterimBillNo = cb.InterimBillNo;
                        view.NetAmount = cb.NetAmount;
                        if (closedLinesByBill.TryGetValue(cb.AdmissionDayBillId, out var ls))
                            view.Lines = ls.OrderBy(l => l.ServiceDate).Select(MapClosedLine).ToList();
                    }
                    else
                    {
                        var list = liveByDay.TryGetValue(d, out var l2) ? l2 : new List<BillingChargeEvent>();
                        view.NetAmount = list.Sum(c => c.NetAmount);
                        view.Lines = list.OrderBy(c => c.ServiceDate).Select(MapLiveLine).ToList();
                    }

                    cumulative += view.NetAmount;
                    view.CumulativeNetAmount = cumulative;
                    days.Add(view);
                }

                return new GetAdmissionDayBillsResponseModel
                {
                    Success = true,
                    Data = new AdmissionDayBillsData
                    {
                        AdmissionId = Guid.Empty,
                        EncounterId = request.EncounterId,
                        PatientId = patientId,
                        AdmittedAt = anchor,
                        TotalDays = totalDays,
                        TotalCharged = totalCharged,
                        TotalReceived = received,
                        Balance = totalCharged - received,
                        Days = days,
                    }
                };
            }
            catch (Exception)
            {
                return new GetAdmissionDayBillsResponseModel { Success = false, Message = "Error loading day bills." };
            }
        }

        // 1-based day index for a timestamp relative to the visit anchor (charges before anchor -> day 1).
        private static int DayIndexOf(DateTime ts, DateTime anchor)
        {
            var hours = (DateTime.SpecifyKind(ts, DateTimeKind.Utc) - anchor).TotalHours;
            var idx = (int)Math.Floor(hours / 24.0) + 1;
            return idx < 1 ? 1 : idx;
        }

        private static bool IsRefund(string? paymentType) =>
            string.Equals(paymentType, BillingConstants.PaymentType.Refund, StringComparison.OrdinalIgnoreCase);

        private static AdmissionDayLineView MapClosedLine(AdmissionDayBillLine l) => new()
        {
            ChargeEventId = l.ChargeEventId,
            CategoryCode = l.CategoryCode,
            DisplayName = l.DisplayName,
            ServiceDate = l.ServiceDate,
            Qty = l.Qty,
            UnitPrice = l.UnitPrice,
            GrossAmount = l.GrossAmount,
            DiscountAmount = l.DiscountAmount,
            TaxAmount = l.TaxAmount,
            NetAmount = l.NetAmount,
        };

        private static AdmissionDayLineView MapLiveLine(BillingChargeEvent c) => new()
        {
            ChargeEventId = c.ChargeEventId,
            CategoryCode = c.CategoryCode,
            DisplayName = c.DisplayName,
            ServiceDate = c.ServiceDate,
            Qty = c.Qty,
            UnitPrice = c.UnitPrice,
            GrossAmount = c.GrossAmount ?? (c.Qty * c.UnitPrice),
            DiscountAmount = c.DiscountAmount ?? 0,
            TaxAmount = c.TaxAmount,
            NetAmount = c.NetAmount,
        };
    }
}
