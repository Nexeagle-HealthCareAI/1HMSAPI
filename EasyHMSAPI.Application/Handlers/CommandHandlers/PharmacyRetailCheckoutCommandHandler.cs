using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class PharmacyRetailCheckoutCommandHandler : IRequestHandler<PharmacyRetailCheckoutCommand, PharmacyRetailCheckoutResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<PharmacyRetailCheckoutCommandHandler> _logger;

        public PharmacyRetailCheckoutCommandHandler(AppDbContext context, IMediator mediator, ILogger<PharmacyRetailCheckoutCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<PharmacyRetailCheckoutResponseModel> Handle(PharmacyRetailCheckoutCommand request, CancellationToken cancellationToken)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
        }

        private async Task<PharmacyRetailCheckoutResponseModel> TryHandleAsync(PharmacyRetailCheckoutCommand request, CancellationToken cancellationToken)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;

                // 1. Create Pharmacy Encounter
                var encounter = new Encounter
                {
                    EncounterId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterTypeCode = AppConstants.VisitType_PHARMACY,
                    SourceType = "RETAIL_WALKIN",
                    PrimaryDoctorId = request.PrescribingDoctorId,
                    StatusCode = BillingConstants.EncounterStatus.Open,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName ?? "System",
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName ?? "System"
                };
                _context.Encounter.Add(encounter);
                await _context.SaveChangesAsync(cancellationToken);

                // 2. Issue Stock & Create Charges
                var chargeDetails = new List<ChargeDetail>();

                foreach (var item in request.Items)
                {
                    // Issue Stock via MediatR (handles batching, expiry checks, etc.)
                    var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                    {
                        HospitalId = request.HospitalId,
                        InventoryItemId = item.InventoryItemId,
                        StoreId = request.StoreId,
                        BatchId = item.BatchId,
                        MovementType = "ISSUE",
                        Qty = item.Qty,
                        EncounterId = encounter.EncounterId,
                        PatientId = request.PatientId,
                        SourceModule = BillingConstants.SourceModule.PharmacyCounter,
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName
                    }, cancellationToken);

                    if (!movementResponse.Success)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"Stock issue failed: {movementResponse.Message}" };
                    }

                    // Look up ChargeId
                    var invItem = await _context.InventoryItem.FindAsync(new object[] { item.InventoryItemId }, cancellationToken);
                    if (invItem?.ChargeId != null)
                    {
                        chargeDetails.Add(new ChargeDetail
                        {
                            ChargeId = invItem.ChargeId,
                            Qty = item.Qty,
                            Rate = item.Rate,
                            DiscountPercent = item.DiscountPercent,
                            CategoryCode = invItem.Category,
                            SourceModule = BillingConstants.SourceModule.PharmacyCounter
                        });
                    }
                }

                if (!chargeDetails.Any())
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = "No billable items in cart." };
                }

                // 3. Post Charges
                var chargeResponse = await _mediator.Send(new AddChargeEventRequestModel
                {
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = encounter.EncounterId,
                    Charges = chargeDetails,
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName
                }, cancellationToken);

                if (chargeResponse.Success != true || chargeResponse.Data?.ChargeEvents == null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"Billing failed: {chargeResponse.Message}" };
                }

                // 4. Create Finalized Invoice
                var chargeEvents = await _context.BillingChargeEvent
                    .Where(ce => chargeResponse.Data.ChargeEvents.Select(c => c.ChargeEventId).Contains(ce.ChargeEventId))
                    .ToListAsync(cancellationToken);

                decimal netAmount = chargeEvents.Sum(c => c.NetAmount);
                decimal taxAmount = chargeEvents.Sum(c => c.TaxAmount);

                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.Invoice, request.LoggedInUserName, cancellationToken);

                numberSeries.CurrentValue++;
                string invoiceNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix,
                    numberSeries.YearFormat,
                    numberSeries.Separator,
                    numberSeries.PadLength,
                    numberSeries.CurrentValue);

                var invoice = new BillingInvoice
                {
                    InvoiceId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = encounter.EncounterId,
                    InvoiceNo = invoiceNo,
                    GrossAmount = chargeEvents.Sum(c => c.GrossAmount ?? 0),
                    DiscountAmount = chargeEvents.Sum(c => c.DiscountAmount ?? 0),
                    TaxAmount = taxAmount,
                    NetAmount = netAmount,
                    StatusCode = BillingConstants.InvoiceStatus.Finalized,
                    InvoiceDate = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName ?? "System",
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName ?? "System"
                };

                _context.BillingInvoice.Add(invoice);

                foreach (var ce in chargeEvents)
                {
                    _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent
                    {
                        InvoiceId = invoice.InvoiceId,
                        ChargeEventId = ce.ChargeEventId
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                // 5. Add Payment if applicable
                if (request.PaidAmount > 0)
                {
                    var paymentResponse = await _mediator.Send(new AddPaymentEventRequestModel
                    {
                        HospitalId = request.HospitalId,
                        PatientId = request.PatientId,
                        EncounterId = encounter.EncounterId,
                        Payment = new PaymentDetail
                        {
                            Amount = request.PaidAmount,
                            PaymentMode = request.PaymentMode ?? "CASH",
                            PaymentType = BillingConstants.PaymentType.Payment,
                            Description = "Retail Pharmacy POS Payment"
                        },
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName
                    }, cancellationToken);

                    if (paymentResponse.Success != true)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"Payment failed: {paymentResponse.Message}" };
                    }
                }

                // Close the Encounter
                encounter.StatusCode = BillingConstants.EncounterStatus.Finalized;
                _context.Encounter.Update(encounter);
                await _context.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                return new PharmacyRetailCheckoutResponseModel
                {
                    Success = true,
                    EncounterId = encounter.EncounterId,
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNo = invoiceNo,
                    ChargeEventId = chargeEvents.First().ChargeEventId
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Pharmacy Retail Checkout failed.");
                return new PharmacyRetailCheckoutResponseModel { Success = false, Message = "An error occurred during checkout." };
            }
        }
    }
}
