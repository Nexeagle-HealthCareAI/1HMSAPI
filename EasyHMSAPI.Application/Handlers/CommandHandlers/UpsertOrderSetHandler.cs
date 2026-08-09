using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertOrderSetHandler : IRequestHandler<UpsertOrderSetRequestModel, UpsertOrderSetResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertOrderSetHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertOrderSetResponseModel> Handle(UpsertOrderSetRequestModel request, CancellationToken cancellationToken)
        {
            UpsertOrderSetResponseModel response = new() { Success = false };
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    response.Message = "Name is required.";
                    return response;
                }

                var lines = (request.Lines ?? new List<OrderSetLineInput>())
                    .Where(l => !string.IsNullOrWhiteSpace(l.ItemName))
                    .ToList();
                if (lines.Count == 0)
                {
                    response.Message = "At least one line is required.";
                    return response;
                }
                if (lines.Any(l => !IpdConstants.ClinicalOrderType.All.Contains(l.OrderType?.Trim().ToUpperInvariant())))
                {
                    response.Message = "Every line must have a valid order type.";
                    return response;
                }

                foreach (var l in lines)
                {
                    l.ItemName = l.ItemName.Trim();
                    l.OrderType = l.OrderType.Trim().ToUpperInvariant();
                }
                var linesJson = JsonSerializer.Serialize(lines);
                var category = string.IsNullOrWhiteSpace(request.Category) ? IpdConstants.OrderSetCategory.PostOp : request.Category.Trim().ToUpperInvariant();

                if (request.OrderSetId.HasValue && request.OrderSetId != Guid.Empty)
                {
                    var existing = await _context.OrderSets
                        .FirstOrDefaultAsync(x => x.OrderSetId == request.OrderSetId && x.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                    {
                        response.Message = $"Order Set with ID {request.OrderSetId} not found.";
                        return response;
                    }

                    existing.Name = request.Name.Trim();
                    existing.Category = category;
                    existing.TemplateLinesJson = linesJson;
                    existing.IsActive = request.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "Order Set updated successfully.";
                    response.OrderSetId = existing.OrderSetId;
                    return response;
                }

                var orderSet = new OrderSet
                {
                    OrderSetId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    Name = request.Name.Trim(),
                    Category = category,
                    TemplateLinesJson = linesJson,
                    IsActive = request.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.OrderSets.Add(orderSet);
                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Order Set created successfully.";
                response.OrderSetId = orderSet.OrderSetId;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
