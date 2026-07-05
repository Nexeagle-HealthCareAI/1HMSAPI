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
    public class PurchaseOrderCommandHandlers :
        IRequestHandler<CreatePurchaseOrderRequestModel, CreatePurchaseOrderResponseModel>,
        IRequestHandler<ApprovePurchaseOrderRequestModel, PurchaseOrderActionResponseModel>,
        IRequestHandler<MarkPurchaseOrderSentRequestModel, PurchaseOrderActionResponseModel>,
        IRequestHandler<CancelPurchaseOrderRequestModel, PurchaseOrderActionResponseModel>
    {
        private readonly AppDbContext _context;

        public PurchaseOrderCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreatePurchaseOrderResponseModel> Handle(CreatePurchaseOrderRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.VendorId == Guid.Empty)
                    return new CreatePurchaseOrderResponseModel { Success = false, Message = "HospitalId and VendorId are required." };
                if (request.Lines.Count == 0)
                    return new CreatePurchaseOrderResponseModel { Success = false, Message = "At least one line is required." };
                if (request.Lines.Any(l => l.Qty <= 0 || l.Rate < 0))
                    return new CreatePurchaseOrderResponseModel { Success = false, Message = "Line quantities must be positive and rates cannot be negative." };

                var vendorExists = await _context.Vendor.AnyAsync(
                    v => v.VendorId == request.VendorId && v.HospitalId == request.HospitalId, cancellationToken);
                if (!vendorExists)
                    return new CreatePurchaseOrderResponseModel { Success = false, Message = "Vendor not found." };

                var itemIds = request.Lines.Select(l => l.InventoryItemId).Distinct().ToList();
                var validItemCount = await _context.InventoryItem.CountAsync(
                    i => i.HospitalId == request.HospitalId && itemIds.Contains(i.InventoryItemId), cancellationToken);
                if (validItemCount != itemIds.Count)
                    return new CreatePurchaseOrderResponseModel { Success = false, Message = "One or more items were not found." };

                var now = DateTime.UtcNow;
                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.PurchaseOrder, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var poNumber = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

                var po = new PurchaseOrder
                {
                    PurchaseOrderId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PoNumber = poNumber,
                    VendorId = request.VendorId,
                    IndentId = null,
                    Status = IpdConstants.PurchaseOrderStatus.Draft,
                    OrderedBy = request.LoggedInUserName,
                    OrderedByUserId = request.LoggedInUserId,
                    OrderedAt = now,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    Notes = request.Notes,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.PurchaseOrder.Add(po);

                foreach (var line in request.Lines)
                {
                    _context.PurchaseOrderLine.Add(new PurchaseOrderLine
                    {
                        PurchaseOrderLineId = Guid.NewGuid(),
                        PurchaseOrderId = po.PurchaseOrderId,
                        InventoryItemId = line.InventoryItemId,
                        Qty = line.Qty,
                        Rate = line.Rate,
                        ReceivedQty = 0,
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new CreatePurchaseOrderResponseModel { Success = true, Message = "Purchase order created.", PurchaseOrderId = po.PurchaseOrderId, PoNumber = po.PoNumber };
            }
            catch (Exception)
            {
                return new CreatePurchaseOrderResponseModel { Success = false, Message = "Error creating purchase order." };
            }
        }

        public async Task<PurchaseOrderActionResponseModel> Handle(ApprovePurchaseOrderRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var po = await _context.PurchaseOrder.FirstOrDefaultAsync(
                    p => p.PurchaseOrderId == request.PurchaseOrderId && p.HospitalId == request.HospitalId, cancellationToken);
                if (po == null)
                    return new PurchaseOrderActionResponseModel { Success = false, Message = "Purchase order not found." };
                if (po.Status != IpdConstants.PurchaseOrderStatus.Draft)
                    return new PurchaseOrderActionResponseModel { Success = false, Message = $"Purchase order is {po.Status.ToLowerInvariant()}, not draft." };

                var now = DateTime.UtcNow;
                po.Status = IpdConstants.PurchaseOrderStatus.Approved;
                po.ApprovedBy = request.LoggedInUserName;
                po.ApprovedByUserId = request.LoggedInUserId;
                po.ApprovedAt = now;
                po.UpdatedAt = now;
                po.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new PurchaseOrderActionResponseModel { Success = true, Message = "Purchase order approved." };
            }
            catch (Exception)
            {
                return new PurchaseOrderActionResponseModel { Success = false, Message = "Error approving purchase order." };
            }
        }

        public async Task<PurchaseOrderActionResponseModel> Handle(MarkPurchaseOrderSentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var po = await _context.PurchaseOrder.FirstOrDefaultAsync(
                    p => p.PurchaseOrderId == request.PurchaseOrderId && p.HospitalId == request.HospitalId, cancellationToken);
                if (po == null)
                    return new PurchaseOrderActionResponseModel { Success = false, Message = "Purchase order not found." };
                if (po.Status != IpdConstants.PurchaseOrderStatus.Approved)
                    return new PurchaseOrderActionResponseModel { Success = false, Message = $"Purchase order is {po.Status.ToLowerInvariant()}, not approved." };

                po.Status = IpdConstants.PurchaseOrderStatus.Sent;
                po.UpdatedAt = DateTime.UtcNow;
                po.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new PurchaseOrderActionResponseModel { Success = true, Message = "Purchase order marked as sent." };
            }
            catch (Exception)
            {
                return new PurchaseOrderActionResponseModel { Success = false, Message = "Error marking purchase order as sent." };
            }
        }

        public async Task<PurchaseOrderActionResponseModel> Handle(CancelPurchaseOrderRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                    return new PurchaseOrderActionResponseModel { Success = false, Message = "A reason is required to cancel a purchase order." };

                var po = await _context.PurchaseOrder.FirstOrDefaultAsync(
                    p => p.PurchaseOrderId == request.PurchaseOrderId && p.HospitalId == request.HospitalId, cancellationToken);
                if (po == null)
                    return new PurchaseOrderActionResponseModel { Success = false, Message = "Purchase order not found." };

                var cancellable = new[] { IpdConstants.PurchaseOrderStatus.Draft, IpdConstants.PurchaseOrderStatus.Approved, IpdConstants.PurchaseOrderStatus.Sent };
                if (!cancellable.Contains(po.Status))
                    return new PurchaseOrderActionResponseModel { Success = false, Message = $"Purchase order is {po.Status.ToLowerInvariant()} and cannot be cancelled." };

                po.Status = IpdConstants.PurchaseOrderStatus.Cancelled;
                po.CancelledReason = request.Reason.Trim();
                po.UpdatedAt = DateTime.UtcNow;
                po.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new PurchaseOrderActionResponseModel { Success = true, Message = "Purchase order cancelled." };
            }
            catch (Exception)
            {
                return new PurchaseOrderActionResponseModel { Success = false, Message = "Error cancelling purchase order." };
            }
        }
    }
}
