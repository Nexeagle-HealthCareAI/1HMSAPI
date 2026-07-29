using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SaveLinkedAbhaAccountHandler : IRequestHandler<SaveLinkedAbhaAccountRequestModel, SaveAbhaAccountResponseModel>
    {
        private readonly AppDbContext _context;

        public SaveLinkedAbhaAccountHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SaveAbhaAccountResponseModel> Handle(SaveLinkedAbhaAccountRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.AbhaNumber))
                return new SaveAbhaAccountResponseModel { Success = false, Message = "Hospital and ABHA number are required." };

            var existing = await _context.AbhaAccount.FirstOrDefaultAsync(
                a => a.HospitalId == request.HospitalId && a.AbhaNumber == request.AbhaNumber, cancellationToken);

            if (existing != null)
            {
                existing.AbhaAddress = request.AbhaAddress ?? existing.AbhaAddress;
                existing.FullName = request.FullName ?? existing.FullName;
                existing.Gender = request.Gender ?? existing.Gender;
                existing.DateOfBirth = request.DateOfBirth ?? existing.DateOfBirth;
                existing.Mobile = request.Mobile ?? existing.Mobile;
                await _context.SaveChangesAsync(cancellationToken);
                return new SaveAbhaAccountResponseModel { Success = true, Message = "ABHA account already on record — details refreshed.", AbhaAccountId = existing.AbhaAccountId };
            }

            var account = new AbhaAccount
            {
                AbhaAccountId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                AbhaNumber = request.AbhaNumber,
                AbhaAddress = request.AbhaAddress,
                FullName = request.FullName,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Mobile = request.Mobile,
                Source = "Login",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.LoggedInUserName
            };
            _context.AbhaAccount.Add(account);
            await _context.SaveChangesAsync(cancellationToken);

            return new SaveAbhaAccountResponseModel { Success = true, Message = "ABHA account linked.", AbhaAccountId = account.AbhaAccountId };
        }
    }
}
