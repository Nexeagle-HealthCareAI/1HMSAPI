using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetExpensesHandler : IRequestHandler<GetExpensesRequestModel, GetExpensesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetExpensesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetExpensesResponseModel> Handle(GetExpensesRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Expenses.Where(e => e.HospitalId == request.HospitalId);

            if (request.FromDate.HasValue)
                query = query.Where(e => e.ExpenseDate >= request.FromDate.Value.Date);
            if (request.ToDate.HasValue)
                query = query.Where(e => e.ExpenseDate <= request.ToDate.Value.Date);
            if (!string.IsNullOrWhiteSpace(request.Category))
                query = query.Where(e => e.CategoryCode == request.Category);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim().ToLower();
                query = query.Where(e =>
                    e.CategoryCode.ToLower().Contains(term) ||
                    (e.Vendor != null && e.Vendor.ToLower().Contains(term)) ||
                    (e.Description != null && e.Description.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var totalAmount = totalCount == 0 ? 0m : await query.SumAsync(e => e.Amount, cancellationToken);
            var pendingAmount = await query.Where(e => e.StatusCode == "PENDING")
                .Select(e => (decimal?)e.Amount).SumAsync(cancellationToken) ?? 0m;
            var categoryCount = await query.Select(e => e.CategoryCode).Distinct().CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(e => new ExpenseItemModel
                {
                    ExpenseId = e.ExpenseId,
                    ExpenseDate = e.ExpenseDate,
                    CategoryCode = e.CategoryCode,
                    Vendor = e.Vendor,
                    Description = e.Description,
                    Amount = e.Amount,
                    PaymentMode = e.PaymentMode,
                    StatusCode = e.StatusCode,
                    ReferenceNo = e.ReferenceNo,
                    Notes = e.Notes,
                    UpdatedAt = e.UpdatedAt,
                    UpdatedBy = e.UpdatedBy
                })
                .ToListAsync(cancellationToken);

            return new GetExpensesResponseModel
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalAmount = totalAmount,
                PendingAmount = pendingAmount,
                CategoryCount = categoryCount
            };
        }
    }
}
