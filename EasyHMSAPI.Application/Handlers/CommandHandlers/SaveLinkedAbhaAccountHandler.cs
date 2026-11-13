using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SaveLinkedAbhaAccountHandler : IRequestHandler<SaveLinkedAbhaAccountRequestModel, SaveAbhaAccountResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IAbhaLinkProofStore _proofs;

        public SaveLinkedAbhaAccountHandler(AppDbContext context, IAbhaLinkProofStore proofs)
        {
            _context = context;
            _proofs = proofs;
        }

        public async Task<SaveAbhaAccountResponseModel> Handle(SaveLinkedAbhaAccountRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.CallerUserId == Guid.Empty)
                return new SaveAbhaAccountResponseModel { Success = false, Message = "Hospital and signed-in user are required." };
            if (!await CallerGuards.IsHospitalMemberAsync(_context, request.CallerUserId, request.HospitalId, cancellationToken))
                return new SaveAbhaAccountResponseModel { Success = false, Message = "You don't have access to this hospital." };

            // The profile must come from ABDM (verified by OTP in this session), not from the request body. One use per token.
            var verified = _proofs.Consume(request.LinkToken, request.CallerUserId, request.HospitalId);
            if (verified == null)
                return new SaveAbhaAccountResponseModel { Success = false, Message = "Verify the ABHA with an OTP first. The verification has expired or was already used." };

            var existing = await _context.AbhaAccount.FirstOrDefaultAsync(
                a => a.HospitalId == request.HospitalId && a.AbhaNumber == verified.AbhaNumber, cancellationToken);

            if (existing != null)
            {
                existing.AbhaAddress = verified.AbhaAddress ?? existing.AbhaAddress;
                existing.FullName = verified.FullName ?? existing.FullName;
                existing.Gender = verified.Gender ?? existing.Gender;
                existing.DateOfBirth = verified.DateOfBirth ?? existing.DateOfBirth;
                existing.Mobile = verified.Mobile ?? existing.Mobile;
                await _context.SaveChangesAsync(cancellationToken);
                return new SaveAbhaAccountResponseModel { Success = true, Message = "ABHA account already on record — details refreshed.", AbhaAccountId = existing.AbhaAccountId };
            }

            var account = new AbhaAccount
            {
                AbhaAccountId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                AbhaNumber = verified.AbhaNumber,
                AbhaAddress = verified.AbhaAddress,
                FullName = verified.FullName,
                Gender = verified.Gender,
                DateOfBirth = verified.DateOfBirth,
                Mobile = verified.Mobile,
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
