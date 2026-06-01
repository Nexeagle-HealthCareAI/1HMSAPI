using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Closes the current open billing day for an admission: snapshots every un-billed posted
    /// charge into a numbered, locked interim bill. Charges added afterwards naturally roll into
    /// the next day's close. The day index is admission-anchored (each day = a 24h window from
    /// AdmittedAt).
    /// </summary>
    public class CloseAdmissionDayHandler : IRequestHandler<CloseAdmissionDayRequestModel, CloseAdmissionDayResponseModel>
    {
        private readonly AppDbContext _context;

        public CloseAdmissionDayHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CloseAdmissionDayResponseModel> Handle(CloseAdmissionDayRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new CloseAdmissionDayResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var adm = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (adm == null)
                    return new CloseAdmissionDayResponseModel { Success = false, Message = "Admission not found." };

                var admittedAt = DateTime.SpecifyKind(adm.AdmittedAt, DateTimeKind.Utc);
                var now = DateTime.UtcNow;

                var closedBills = await _context.AdmissionDayBill
                    .Where(b => b.AdmissionId == adm.AdmissionId && b.HospitalId == request.HospitalId
                                && b.StatusCode == BillingConstants.DayBillStatus.Closed)
                    .ToListAsync(cancellationToken);

                var maxClosedDay = closedBills.Count > 0 ? closedBills.Max(b => b.DayNumber) : 0;
                var dayNumber = maxClosedDay + 1;

                var closedIds = closedBills.Select(b => b.AdmissionDayBillId).ToList();
                var billedChargeIds = closedIds.Count == 0
                    ? new HashSet<Guid>()
                    : (await _context.AdmissionDayBillLine
                        .Where(l => closedIds.Contains(l.AdmissionDayBillId))
                        .Select(l => l.ChargeEventId)
                        .ToListAsync(cancellationToken)).ToHashSet();

                var charges = await _context.BillingChargeEvent
                    .Where(c => c.EncounterId == adm.EncounterId && c.HospitalId == request.HospitalId
                                && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                    .ToListAsync(cancellationToken);

                var unbilled = charges
                    .Where(c => !billedChargeIds.Contains(c.ChargeEventId))
                    .OrderBy(c => c.ServiceDate)
                    .ToList();

                if (unbilled.Count == 0)
                    return new CloseAdmissionDayResponseModel { Success = false, Message = "No new charges to bill for this day." };

                var from = admittedAt.AddDays(dayNumber - 1);
                var to = from.AddDays(1);

                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.InterimBill, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var interimBillNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                numberSeries.UpdatedAt = now;
                numberSeries.UpdatedBy = request.LoggedInUserName;

                var billId = Guid.NewGuid();
                decimal gross = 0, discount = 0, tax = 0, net = 0;
                var lines = new List<AdmissionDayBillLine>();
                foreach (var c in unbilled)
                {
                    var lineGross = c.GrossAmount ?? (c.Qty * c.UnitPrice);
                    var lineDiscount = c.DiscountAmount ?? 0;
                    gross += lineGross;
                    discount += lineDiscount;
                    tax += c.TaxAmount;
                    net += c.NetAmount;

                    lines.Add(new AdmissionDayBillLine
                    {
                        AdmissionDayBillLineId = Guid.NewGuid(),
                        AdmissionDayBillId = billId,
                        HospitalId = request.HospitalId,
                        ChargeEventId = c.ChargeEventId,
                        CategoryCode = c.CategoryCode,
                        DisplayName = c.DisplayName,
                        ServiceDate = c.ServiceDate,
                        Qty = c.Qty,
                        UnitPrice = c.UnitPrice,
                        GrossAmount = lineGross,
                        DiscountAmount = lineDiscount,
                        TaxAmount = c.TaxAmount,
                        NetAmount = c.NetAmount,
                        CreatedAt = now,
                    });
                }

                var priorCumulative = closedBills.Sum(b => b.NetAmount);
                var cumulative = priorCumulative + net;

                var payments = await _context.BillingPayment
                    .Where(p => p.EncounterId == adm.EncounterId && p.HospitalId == request.HospitalId)
                    .Select(p => new { p.PaymentType, p.Amount })
                    .ToListAsync(cancellationToken);
                var received = payments.Sum(p =>
                    string.Equals(p.PaymentType, BillingConstants.PaymentType.Refund, StringComparison.OrdinalIgnoreCase)
                        ? -p.Amount : p.Amount);

                var bill = new AdmissionDayBill
                {
                    AdmissionDayBillId = billId,
                    HospitalId = request.HospitalId,
                    AdmissionId = adm.AdmissionId,
                    EncounterId = adm.EncounterId,
                    PatientId = adm.PatientId,
                    DayNumber = dayNumber,
                    FromUtc = from,
                    ToUtc = to,
                    InterimBillNo = interimBillNo,
                    LineCount = lines.Count,
                    GrossAmount = gross,
                    DiscountAmount = discount,
                    TaxAmount = tax,
                    NetAmount = net,
                    CumulativeNetAmount = cumulative,
                    AdvanceReceived = received,
                    BalanceDue = cumulative - received,
                    StatusCode = BillingConstants.DayBillStatus.Closed,
                    ClosedAt = now,
                    ClosedBy = request.LoggedInUserName,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };

                _context.AdmissionDayBill.Add(bill);
                _context.AdmissionDayBillLine.AddRange(lines);
                await _context.SaveChangesAsync(cancellationToken);

                return new CloseAdmissionDayResponseModel
                {
                    Success = true,
                    Message = $"Day {dayNumber} closed. Interim bill {interimBillNo} for ₹{net:0.00}.",
                    AdmissionDayBillId = billId,
                    DayNumber = dayNumber,
                    InterimBillNo = interimBillNo,
                    NetAmount = net,
                    BalanceDue = cumulative - received,
                };
            }
            catch (Exception)
            {
                return new CloseAdmissionDayResponseModel { Success = false, Message = "Error closing admission day." };
            }
        }
    }
}
