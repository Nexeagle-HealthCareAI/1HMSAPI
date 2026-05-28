using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertExpenseHandler : IRequestHandler<UpsertExpenseRequestModel, UpsertExpenseResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertExpenseHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertExpenseResponseModel> Handle(UpsertExpenseRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CategoryCode))
                throw new ArgumentException("Category is required.");
            if (request.Amount < 0)
                throw new ArgumentException("Amount cannot be negative.");

            var now = DateTime.UtcNow;
            var category = request.CategoryCode.Trim();
            var status = string.IsNullOrWhiteSpace(request.StatusCode) ? "PAID" : request.StatusCode.Trim().ToUpperInvariant();
            var date = (request.ExpenseDate ?? now).Date;

            if (request.ExpenseId.HasValue && request.ExpenseId != Guid.Empty)
            {
                var existing = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.ExpenseId == request.ExpenseId && e.HospitalId == request.HospitalId, cancellationToken);
                if (existing == null)
                    throw new Exception("Expense not found for update.");

                existing.ExpenseDate = date;
                existing.CategoryCode = category;
                existing.Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim();
                existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                existing.Amount = request.Amount;
                existing.PaymentMode = string.IsNullOrWhiteSpace(request.PaymentMode) ? null : request.PaymentMode.Trim().ToUpperInvariant();
                existing.StatusCode = status;
                existing.ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim();
                existing.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                existing.UpdatedAt = now;
                existing.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new UpsertExpenseResponseModel { ExpenseId = existing.ExpenseId, Message = "Expense updated successfully." };
            }

            var expense = new Expense
            {
                ExpenseId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                ExpenseDate = date,
                CategoryCode = category,
                Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Amount = request.Amount,
                PaymentMode = string.IsNullOrWhiteSpace(request.PaymentMode) ? null : request.PaymentMode.Trim().ToUpperInvariant(),
                StatusCode = status,
                ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                CreatedBy = request.LoggedInUserName,
                UpdatedAt = now,
                UpdatedBy = request.LoggedInUserName
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync(cancellationToken);
            return new UpsertExpenseResponseModel { ExpenseId = expense.ExpenseId, Message = "Expense created successfully." };
        }
    }
}
