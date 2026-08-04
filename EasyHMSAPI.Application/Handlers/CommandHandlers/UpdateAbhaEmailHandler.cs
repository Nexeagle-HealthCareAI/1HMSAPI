using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateAbhaEmailHandler : IRequestHandler<UpdateAbhaEmailRequestModel, AbdmUpdateResponseModel>
    {
        private readonly IAbdmAbhaService _abha;
        private readonly AppDbContext _context;

        public UpdateAbhaEmailHandler(IAbdmAbhaService abha, AppDbContext context)
        {
            _abha = abha;
            _context = context;
        }

        public async Task<AbdmUpdateResponseModel> Handle(UpdateAbhaEmailRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionTxnId) || string.IsNullOrWhiteSpace(request.NewEmail))
                return new AbdmUpdateResponseModel { Success = false, Message = "Session and email are required." };

            try
            {
                await _abha.UpdateEmailAsync(request.SessionTxnId, request.NewEmail, cancellationToken);

                var account = await _context.AbhaAccount.FirstOrDefaultAsync(
                    a => a.HospitalId == request.HospitalId && a.AbhaNumber == request.AbhaNumber, cancellationToken);
                if (account != null)
                {
                    account.Email = request.NewEmail;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // ABDM's own record is updated regardless (that already happened above) — but if there's
                // no local AbhaAccount row for this (HospitalId, AbhaNumber), say so explicitly instead
                // of silently reporting a plain success that implies everything, including the local
                // copy, is now in sync.
                var message = account == null
                    ? "Email updated on ABDM, but no local ABHA record was found to update at this hospital."
                    : "Email updated.";
                return new AbdmUpdateResponseModel { Success = true, Message = message, Email = request.NewEmail };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmUpdateResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
