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
    public class IndentCommandHandlers :
        IRequestHandler<CreateIndentRequestModel, CreateIndentResponseModel>,
        IRequestHandler<SubmitIndentRequestModel, ApproveIndentResponseModel>,
        IRequestHandler<ApproveIndentRequestModel, ApproveIndentResponseModel>,
        IRequestHandler<ConvertIndentToPoRequestModel, ConvertIndentToPoResponseModel>,
        IRequestHandler<IssueIndentRequestModel, IssueIndentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public IndentCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<CreateIndentResponseModel> Handle(CreateIndentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.RequestingStoreId == Guid.Empty)
                    return new CreateIndentResponseModel { Success = false, Message = "HospitalId and RequestingStoreId are required." };
                if (request.Lines.Count == 0)
                    return new CreateIndentResponseModel { Success = false, Message = "At least one line is required." };
                if (request.Lines.Any(l => l.Qty <= 0))
                    return new CreateIndentResponseModel { Success = false, Message = "All line quantities must be greater than zero." };

                var storeExists = await _context.Store.AnyAsync(
                    s => s.StoreId == request.RequestingStoreId && s.HospitalId == request.HospitalId, cancellationToken);
                if (!storeExists)
                    return new CreateIndentResponseModel { Success = false, Message = "Requesting store not found." };

                if (request.TargetStoreId.HasValue)
                {
                    var targetStoreExists = await _context.Store.AnyAsync(
                        s => s.StoreId == request.TargetStoreId.Value && s.HospitalId == request.HospitalId, cancellationToken);
                    if (!targetStoreExists)
                        return new CreateIndentResponseModel { Success = false, Message = "Target store not found." };
                }

                var itemIds = request.Lines.Select(l => l.InventoryItemId).Distinct().ToList();
                var validItemCount = await _context.InventoryItem.CountAsync(
                    i => i.HospitalId == request.HospitalId && itemIds.Contains(i.InventoryItemId), cancellationToken);
                if (validItemCount != itemIds.Count)
                    return new CreateIndentResponseModel { Success = false, Message = "One or more items were not found." };

                var now = DateTime.UtcNow;
                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.Indent, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var indentNumber = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

                var indent = new Indent
                {
                    IndentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    IndentNumber = indentNumber,
                    RequestingStoreId = request.RequestingStoreId,
                    TargetStoreId = request.TargetStoreId,
                    Status = request.IsSystemGenerated ? IpdConstants.IndentStatus.Draft : IpdConstants.IndentStatus.Submitted,
                    IsSystemGenerated = request.IsSystemGenerated,
                    RequestedBy = request.LoggedInUserName,
                    RequestedByUserId = request.LoggedInUserId,
                    RequestedAt = now,
                    Notes = request.Notes,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Indent.Add(indent);

                foreach (var line in request.Lines)
                {
                    _context.IndentLine.Add(new IndentLine
                    {
                        IndentLineId = Guid.NewGuid(),
                        IndentId = indent.IndentId,
                        InventoryItemId = line.InventoryItemId,
                        Qty = line.Qty,
                        Notes = line.Notes,
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateIndentResponseModel { Success = true, Message = "Indent created.", IndentId = indent.IndentId, IndentNumber = indent.IndentNumber };
            }
            catch (Exception)
            {
                return new CreateIndentResponseModel { Success = false, Message = "Error creating indent." };
            }
        }

        public async Task<ApproveIndentResponseModel> Handle(SubmitIndentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var indent = await _context.Indent.FirstOrDefaultAsync(
                    i => i.IndentId == request.IndentId && i.HospitalId == request.HospitalId, cancellationToken);
                if (indent == null)
                    return new ApproveIndentResponseModel { Success = false, Message = "Indent not found." };
                if (indent.Status != IpdConstants.IndentStatus.Draft)
                    return new ApproveIndentResponseModel { Success = false, Message = $"Indent is {indent.Status.ToLowerInvariant()}, not draft." };

                indent.Status = IpdConstants.IndentStatus.Submitted;
                indent.UpdatedAt = DateTime.UtcNow;
                indent.UpdatedBy = request.LoggedInUserName;
                await _context.SaveChangesAsync(cancellationToken);

                return new ApproveIndentResponseModel { Success = true, Message = "Indent submitted." };
            }
            catch (Exception)
            {
                return new ApproveIndentResponseModel { Success = false, Message = "Error submitting indent." };
            }
        }

        public async Task<ApproveIndentResponseModel> Handle(ApproveIndentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (!request.Approve && string.IsNullOrWhiteSpace(request.Reason))
                    return new ApproveIndentResponseModel { Success = false, Message = "A reason is required to reject an indent." };

                var indent = await _context.Indent.FirstOrDefaultAsync(
                    i => i.IndentId == request.IndentId && i.HospitalId == request.HospitalId, cancellationToken);
                if (indent == null)
                    return new ApproveIndentResponseModel { Success = false, Message = "Indent not found." };
                if (indent.Status != IpdConstants.IndentStatus.Submitted)
                    return new ApproveIndentResponseModel { Success = false, Message = $"Indent is {indent.Status.ToLowerInvariant()}, not submitted." };

                var now = DateTime.UtcNow;
                indent.Status = request.Approve ? IpdConstants.IndentStatus.Approved : IpdConstants.IndentStatus.Rejected;
                indent.ApprovedBy = request.LoggedInUserName;
                indent.ApprovedByUserId = request.LoggedInUserId;
                indent.ApprovedAt = now;
                if (!request.Approve)
                    indent.RejectedReason = request.Reason!.Trim();
                indent.UpdatedAt = now;
                indent.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ApproveIndentResponseModel { Success = true, Message = request.Approve ? "Indent approved." : "Indent rejected." };
            }
            catch (Exception)
            {
                return new ApproveIndentResponseModel { Success = false, Message = "Error deciding indent." };
            }
        }

        public async Task<ConvertIndentToPoResponseModel> Handle(ConvertIndentToPoRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.VendorId == Guid.Empty)
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "VendorId is required." };
                if (request.Lines.Count == 0)
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "At least one line is required." };
                if (request.Lines.Any(l => l.Rate < 0))
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "Rate cannot be negative." };

                var indent = await _context.Indent.FirstOrDefaultAsync(
                    i => i.IndentId == request.IndentId && i.HospitalId == request.HospitalId, cancellationToken);
                if (indent == null)
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "Indent not found." };
                if (indent.Status != IpdConstants.IndentStatus.Approved)
                    return new ConvertIndentToPoResponseModel { Success = false, Message = $"Indent is {indent.Status.ToLowerInvariant()}, not approved." };

                var vendorExists = await _context.Vendor.AnyAsync(
                    v => v.VendorId == request.VendorId && v.HospitalId == request.HospitalId, cancellationToken);
                if (!vendorExists)
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "Vendor not found." };

                var indentLines = await _context.IndentLine.Where(l => l.IndentId == indent.IndentId).ToListAsync(cancellationToken);
                var indentLinesById = indentLines.ToDictionary(l => l.IndentLineId);
                if (request.Lines.Any(l => !indentLinesById.ContainsKey(l.IndentLineId)))
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "One or more lines do not belong to this indent." };
                if (request.Lines.Select(l => l.IndentLineId).Distinct().Count() != indentLines.Count)
                    return new ConvertIndentToPoResponseModel { Success = false, Message = "A rate must be supplied for every indent line." };

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
                    IndentId = indent.IndentId,
                    Status = IpdConstants.PurchaseOrderStatus.Draft,
                    OrderedBy = request.LoggedInUserName,
                    OrderedAt = now,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.PurchaseOrder.Add(po);

                foreach (var line in request.Lines)
                {
                    var indentLine = indentLinesById[line.IndentLineId];
                    _context.PurchaseOrderLine.Add(new PurchaseOrderLine
                    {
                        PurchaseOrderLineId = Guid.NewGuid(),
                        PurchaseOrderId = po.PurchaseOrderId,
                        InventoryItemId = indentLine.InventoryItemId,
                        Qty = indentLine.Qty,
                        Rate = line.Rate,
                        ReceivedQty = 0,
                    });
                }

                indent.Status = IpdConstants.IndentStatus.ConvertedToPo;
                indent.UpdatedAt = now;
                indent.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ConvertIndentToPoResponseModel { Success = true, Message = "Purchase order created.", PurchaseOrderId = po.PurchaseOrderId, PoNumber = po.PoNumber };
            }
            catch (Exception)
            {
                return new ConvertIndentToPoResponseModel { Success = false, Message = "Error converting indent to purchase order." };
            }
        }

        public async Task<IssueIndentResponseModel> Handle(IssueIndentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.IndentId == Guid.Empty)
                    return new IssueIndentResponseModel { Success = false, Message = "HospitalId and IndentId are required." };
                if (request.Lines.Count == 0)
                    return new IssueIndentResponseModel { Success = false, Message = "At least one line is required to issue." };
                if (request.Lines.Any(l => l.Qty <= 0))
                    return new IssueIndentResponseModel { Success = false, Message = "Issue quantities must be positive." };

                var indent = await _context.Indent.FirstOrDefaultAsync(
                    i => i.IndentId == request.IndentId && i.HospitalId == request.HospitalId, cancellationToken);
                if (indent == null)
                    return new IssueIndentResponseModel { Success = false, Message = "Indent not found." };
                if (indent.Status != IpdConstants.IndentStatus.Submitted)
                    return new IssueIndentResponseModel { Success = false, Message = $"Indent is {indent.Status.ToLowerInvariant()}, not submitted." };
                if (!indent.TargetStoreId.HasValue)
                    return new IssueIndentResponseModel { Success = false, Message = "Indent has no target store assigned for internal transfer." };

                var indentLines = await _context.IndentLine.Where(l => l.IndentId == indent.IndentId).ToListAsync(cancellationToken);
                var indentLinesById = indentLines.ToDictionary(l => l.IndentLineId);
                
                if (request.Lines.Any(l => !indentLinesById.ContainsKey(l.IndentLineId)))
                    return new IssueIndentResponseModel { Success = false, Message = "One or more lines do not belong to this indent." };

                // Execute transfer for each line
                foreach (var issueLine in request.Lines)
                {
                    var indentLine = indentLinesById[issueLine.IndentLineId];
                    
                    var transferResponse = await _mediator.Send(new TransferStockRequestModel
                    {
                        HospitalId = request.HospitalId,
                        FromStoreId = indent.TargetStoreId.Value,
                        ToStoreId = indent.RequestingStoreId,
                        InventoryItemId = indentLine.InventoryItemId,
                        BatchId = issueLine.BatchId,
                        Qty = issueLine.Qty,
                        Notes = $"Issued against Indent {indent.IndentNumber}",
                        LoggedInUserName = request.LoggedInUserName,
                        LoggedInUserId = request.LoggedInUserId
                    }, cancellationToken);

                    if (!transferResponse.Success)
                    {
                        // Stop processing further lines
                        return new IssueIndentResponseModel { Success = false, Message = transferResponse.Message ?? "Failed to issue stock." };
                    }
                }

                // Mark Indent as ISSUED
                indent.Status = IpdConstants.IndentStatus.Issued;
                indent.UpdatedAt = DateTime.UtcNow;
                indent.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new IssueIndentResponseModel { Success = true, Message = "Stock issued successfully." };
            }
            catch (Exception ex)
            {
                return new IssueIndentResponseModel { Success = false, Message = $"Error processing issue: {ex.Message}" };
            }
        }
    }
}
