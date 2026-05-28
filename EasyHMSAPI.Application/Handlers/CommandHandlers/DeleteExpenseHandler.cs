using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteExpenseHandler : IRequestHandler<DeleteExpenseRequestModel, DeleteExpenseResponseModel>
    {
        private readonly AppDbContext _context;

        public DeleteExpenseHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DeleteExpenseResponseModel> Handle(DeleteExpenseRequestModel request, CancellationToken cancellationToken)
        {
            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.ExpenseId == request.ExpenseId && e.HospitalId == request.HospitalId, cancellationToken);

            if (expense == null)
                return new DeleteExpenseResponseModel { IsSuccess = false, Message = "Expense not found." };

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync(cancellationToken);
            return new DeleteExpenseResponseModel { IsSuccess = true, Message = "Expense deleted." };
        }
    }
}
