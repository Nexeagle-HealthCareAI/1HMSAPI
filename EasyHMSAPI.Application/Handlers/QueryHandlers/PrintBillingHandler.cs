using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class PrintBillingHandler : IRequestHandler<PrintBillingRequestModel, PrintBillingResponseModel>
    {
        private readonly AppDbContext _context;

        public PrintBillingHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PrintBillingResponseModel> Handle(PrintBillingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var hospital = await _context.Hospitals
                    .Where(h => h.HospitalID == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (hospital == null)
                {
                    return new PrintBillingResponseModel
                    {
                        Success = false,
                        Message = "Hospital not found."
                    };
                }

                var billingInvoice = await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == request.EncounterId && bi.HospitalId == request.HospitalId && bi.PatientId == request.PatientId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (billingInvoice == null)
                {
                    return new PrintBillingResponseModel
                    {
                        Success = false,
                        Message = "Billing invoice not found."
                    };
                }

                var chargeEvents = await _context.BillingInvoiceChargeEvent
                    .Where(bice => bice.InvoiceId == billingInvoice.InvoiceId)
                    .Join(_context.BillingChargeEvent,
                          bice => bice.ChargeEventId,
                          bce => bce.ChargeEventId,
                          (bice, bce) => bce)
                    .OrderBy(bce => bce.CreatedAt)
                    .ToListAsync(cancellationToken);

                var chargeDetails = chargeEvents.Select(ce => new PrintBillingChargeDetail
                {
                    DisplayName = ce.DisplayName,
                    Qty = ce.Qty,
                    UnitPrice = ce.UnitPrice,
                    GrossAmount = ce.GrossAmount ?? 0,
                    DiscountAmount = ce.DiscountAmount ?? 0,
                    NetAmount = ce.NetAmount
                }).ToList();

                var paymentAllocations = await _context.BillingPaymentAllocation
                    .Where(bpa => bpa.InvoiceId == billingInvoice.InvoiceId)
                    .Join(_context.BillingPayment,
                          bpa => bpa.PaymentId,
                          bp => bp.PaymentId,
                          (bpa, bp) => bp)
                    .OrderBy(bp => bp.CreatedAt)
                    .ToListAsync(cancellationToken);

                var paymentDetails = paymentAllocations.Select(p => new PrintBillingPaymentDetail
                {
                    ReceiptNo = p.ReceiptNo,
                    PaymentType = p.PaymentType,
                    PaymentMode = p.PaymentMode,
                    Amount = p.Amount
                }).ToList();

                var hospitalInfo = new HospitalInfo
                {
                    HospitalId = hospital.HospitalID,
                    Name = hospital.Name,
                    Type = hospital.Type,
                    Email = hospital.Email,
                    Contact = hospital.Contact,
                    AlternateContact = hospital.AlternateContact,
                    Website = hospital.Website,
                    Location = hospital.Location,
                    City = hospital.City,
                    State = hospital.State,
                    Country = hospital.Country,
                    Pincode = hospital.Pincode,
                    GSTIN = hospital.GSTIN,
                    PAN = hospital.PAN,
                    NABH_NABL = hospital.NABH_NABL
                };

                var invoiceInfo = new InvoiceInfo
                {
                    InvoiceNo = billingInvoice.InvoiceNo,
                    InvoiceDate = billingInvoice.InvoiceDate,
                    GrossAmount = billingInvoice.GrossAmount ?? 0,
                    DiscountAmount = billingInvoice.DiscountAmount ?? 0,
                    NetAmount = billingInvoice.NetAmount ?? 0
                };

                return new PrintBillingResponseModel
                {
                    Success = true,
                    Message = "Billing data retrieved successfully.",
                    Data = new PrintBillingData
                    {
                        Hospital = hospitalInfo,
                        Invoice = invoiceInfo,
                        Charges = chargeDetails,
                        Payments = paymentDetails
                    }
                };
            }
            catch (Exception)
            {
                return new PrintBillingResponseModel
                {
                    Success = false,
                    Message = "Error retrieving billing data."
                };
            }
        }
    }
}
