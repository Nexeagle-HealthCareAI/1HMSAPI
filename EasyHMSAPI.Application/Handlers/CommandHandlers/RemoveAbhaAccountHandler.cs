using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RemoveAbhaAccountHandler : IRequestHandler<RemoveAbhaAccountRequestModel, RemoveAbhaAccountResponseModel>
    {
        private readonly AppDbContext _context;

        public RemoveAbhaAccountHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RemoveAbhaAccountResponseModel> Handle(RemoveAbhaAccountRequestModel request, CancellationToken cancellationToken)
        {
            var account = await _context.AbhaAccount
                .FirstOrDefaultAsync(a => a.AbhaAccountId == request.AbhaAccountId && a.HospitalId == request.HospitalId, cancellationToken);

            if (account == null)
                return new RemoveAbhaAccountResponseModel { Success = false, Message = "ABHA account not found." };

            _context.AbhaAccount.Remove(account);
            await _context.SaveChangesAsync(cancellationToken);

            return new RemoveAbhaAccountResponseModel { Success = true, Message = "Removed from hospital records." };
        }
    }
}
