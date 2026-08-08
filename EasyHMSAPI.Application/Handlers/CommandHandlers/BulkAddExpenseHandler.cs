using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class BulkAddExpenseHandler : IRequestHandler<BulkAddExpenseRequestModel, BulkAddExpenseResponseModel>
    {
        private readonly AppDbContext _context;

        public BulkAddExpenseHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BulkAddExpenseResponseModel> Handle(BulkAddExpenseRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.CategoryCode))
                return new BulkAddExpenseResponseModel { Success = false, Message = "HospitalId and CategoryCode are required." };

            if (request.Lines == null || request.Lines.Count == 0)
                return new BulkAddExpenseResponseModel { Success = false, Message = "At least one expense line is required." };

            if (request.Lines.Any(l => l.Amount <= 0))
                return new BulkAddExpenseResponseModel { Success = false, Message = "Every line must have an amount greater than zero." };

            var now = DateTime.UtcNow;
            var category = request.CategoryCode.Trim();
            var status = string.IsNullOrWhiteSpace(request.StatusCode) ? "PAID" : request.StatusCode.Trim().ToUpperInvariant();
            var date = (request.ExpenseDate ?? now).Date;
            var vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim();
            var paymentMode = string.IsNullOrWhiteSpace(request.PaymentMode) ? null : request.PaymentMode.Trim().ToUpperInvariant();

            var expenses = request.Lines.Select(line => new Expense
            {
                ExpenseId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                ExpenseDate = date,
                CategoryCode = category,
                Vendor = vendor,
                Amount = line.Amount,
                PaymentMode = paymentMode,
                StatusCode = status,
                // Reason is stored on Notes -- the same field the single-expense form already
                // carries, just surfaced here under the label the bulk-entry flow uses it for.
                Notes = string.IsNullOrWhiteSpace(line.Reason) ? null : line.Reason.Trim(),
                CreatedAt = now,
                CreatedBy = request.LoggedInUserName,
                UpdatedAt = now,
                UpdatedBy = request.LoggedInUserName,
            }).ToList();

            _context.Expenses.AddRange(expenses);
            await _context.SaveChangesAsync(cancellationToken);

            return new BulkAddExpenseResponseModel
            {
                Success = true,
                Message = $"{expenses.Count} expense(s) added.",
                CreatedCount = expenses.Count,
                ExpenseIds = expenses.Select(e => e.ExpenseId).ToList(),
            };
        }
    }
}
