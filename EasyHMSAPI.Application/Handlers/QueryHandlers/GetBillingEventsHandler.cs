using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBillingEventsHandler : IRequestHandler<GetBillingEventsRequestModel, GetBillingEventsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBillingEventsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBillingEventsResponseModel> Handle(GetBillingEventsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // All charges for this encounter (whether or not linked to an invoice)
                var charges = await _context.BillingChargeEvent
                    .Where(ce => ce.EncounterId == request.EncounterId
                              && ce.HospitalId == request.HospitalId
                              && ce.PatientId == request.PatientId)
                    .OrderBy(ce => ce.CreatedAt)
                    .ToListAsync(cancellationToken);

                // Which of those are already linked to any invoice
                var chargeIds = charges.Select(c => c.ChargeEventId).ToList();
                var invoicedChargeIds = chargeIds.Count == 0
                    ? new HashSet<Guid>()
                    : (await _context.BillingInvoiceChargeEvent
                        .Where(bice => chargeIds.Contains(bice.ChargeEventId))
                        .Select(bice => bice.ChargeEventId)
                        .ToListAsync(cancellationToken)).ToHashSet();

                // Most recent invoice for this encounter (DRAFT preferred, else latest)
                var invoices = await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == request.EncounterId
                              && bi.HospitalId == request.HospitalId
                              && bi.PatientId == request.PatientId)
                    .OrderByDescending(bi => bi.CreatedAt)
                    .ToListAsync(cancellationToken);

                var currentInvoice = invoices.FirstOrDefault(i => i.StatusCode == BillingConstants.InvoiceStatus.Draft)
                                     ?? invoices.FirstOrDefault();

                // All payments for this encounter (sum across all invoices for the encounter)
                var payments = await _context.BillingPayment
                    .Where(p => p.EncounterId == request.EncounterId
                             && p.HospitalId == request.HospitalId
                             && p.PatientId == request.PatientId)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync(cancellationToken);

                var chargeDetails = charges.Select(ce => new BillingChargeDetail
                {
                    ChargeEventId = ce.ChargeEventId,
                    CreatedDateTime = ce.CreatedAt,
                    DisplayName = ce.DisplayName,
                    CategoryCode = ce.CategoryCode,
                    SourceModule = ce.SourceModule,
                    Rate = ce.UnitPrice,
                    Qty = ce.Qty,
                    GrossAmount = ce.GrossAmount ?? (ce.Qty * ce.UnitPrice),
                    DiscountAmount = ce.DiscountAmount ?? 0,
                    NetAmount = ce.NetAmount,
                    HsnSacCode = ce.HsnSacCode,
                    GstRate = ce.GstRate,
                    TaxableAmount = ce.TaxableAmount,
                    CgstAmount = ce.CgstAmount,
                    SgstAmount = ce.SgstAmount,
                    IgstAmount = ce.IgstAmount,
                    TaxAmount = ce.TaxAmount,
                    IsTaxInclusive = ce.IsTaxInclusive,
                    IsInterState = ce.IsInterState,
                    StatusCode = ce.StatusCode,
                    IsInvoiced = invoicedChargeIds.Contains(ce.ChargeEventId)
                }).ToList();

                var paymentDetails = payments.Select(p => new BillingPaymentDetail
                {
                    PaymentId = p.PaymentId,
                    CreatedDateTime = p.CreatedAt,
                    PaymentType = p.PaymentType,
                    PaymentMode = p.PaymentMode,
                    PaymentDescription = p.PaymentDescription,
                    ReceiptNo = p.ReceiptNo,
                    Amount = p.Amount
                }).ToList();

                // Totals: posted (non-VOID) charges count toward "billed" so the UI can show
                // a running total even before the user clicks "Generate Bill".
                decimal totalCharges = charges
                    .Where(c => c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                    .Sum(c => c.NetAmount);
                decimal totalReceived = payments
                    .Where(p => p.PaymentType != BillingConstants.PaymentType.Refund)
                    .Sum(p => p.Amount)
                    - payments
                    .Where(p => p.PaymentType == BillingConstants.PaymentType.Refund)
                    .Sum(p => p.Amount);
                decimal netBalance = totalCharges - totalReceived;

                CurrentInvoiceInfo? invoiceInfo = currentInvoice == null ? null : new CurrentInvoiceInfo
                {
                    InvoiceId = currentInvoice.InvoiceId,
                    InvoiceNo = currentInvoice.InvoiceNo,
                    StatusCode = currentInvoice.StatusCode,
                    InvoiceDate = currentInvoice.InvoiceDate,
                    FinalizedAt = currentInvoice.FinalizedAt,
                    FinalizedBy = currentInvoice.FinalizedBy,
                    GrossAmount = currentInvoice.GrossAmount,
                    DiscountAmount = currentInvoice.DiscountAmount,
                    NetAmount = currentInvoice.NetAmount,
                    TaxableAmount = currentInvoice.TaxableAmount,
                    CgstAmount = currentInvoice.CgstAmount,
                    SgstAmount = currentInvoice.SgstAmount,
                    IgstAmount = currentInvoice.IgstAmount,
                    TaxAmount = currentInvoice.TaxAmount,
                    BuyerGstin = currentInvoice.BuyerGstin,
                    PlaceOfSupplyStateCode = currentInvoice.PlaceOfSupplyStateCode,
                    IsReopened = currentInvoice.IsReopened
                };

                return new GetBillingEventsResponseModel
                {
                    Success = true,
                    Message = "Billing events retrieved successfully.",
                    Data = new GetBillingEventsData
                    {
                        TotalBilledAmount = totalCharges,
                        AmountReceived = totalReceived,
                        NetBalance = netBalance,
                        CurrentInvoice = invoiceInfo,
                        Charges = chargeDetails,
                        Payments = paymentDetails
                    }
                };
            }
            catch (Exception)
            {
                return new GetBillingEventsResponseModel
                {
                    Success = false,
                    Message = "Error retrieving billing events."
                };
            }
        }
    }
}
