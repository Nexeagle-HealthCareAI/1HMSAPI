using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Finalizes ABHA creation by registering the chosen/custom ABHA address, then persists
    /// the resulting account so it shows up on the ABDM dashboard.</summary>
    public class CreateAbhaAddressHandler : IRequestHandler<CreateAbhaAddressRequestModel, AbdmEnrollResponseModel>
    {
        private readonly IAbdmAbhaService _abha;
        private readonly AppDbContext _context;

        public CreateAbhaAddressHandler(IAbdmAbhaService abha, AppDbContext context)
        {
            _abha = abha;
            _context = context;
        }

        public async Task<AbdmEnrollResponseModel> Handle(CreateAbhaAddressRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TxnId) || string.IsNullOrWhiteSpace(request.AbhaAddress))
                return new AbdmEnrollResponseModel { Success = false, Message = "Transaction and ABHA address are required." };

            try
            {
                var result = await _abha.CreateAbhaAddressAsync(request.TxnId, request.AbhaAddress, cancellationToken);

                var account = new AbhaAccount
                {
                    AbhaAccountId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AbhaNumber = result.AbhaNumber,
                    AbhaAddress = result.AbhaAddress,
                    FullName = result.FullName,
                    Gender = result.Gender,
                    DateOfBirth = result.DateOfBirth,
                    Mobile = result.Mobile,
                    Source = "AadhaarEnrol",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName
                };
                _context.AbhaAccount.Add(account);
                await _context.SaveChangesAsync(cancellationToken);

                return new AbdmEnrollResponseModel
                {
                    Success = true,
                    TxnId = result.TxnId,
                    AbhaNumber = result.AbhaNumber,
                    AbhaAddress = result.AbhaAddress,
                    FullName = result.FullName,
                    Gender = result.Gender,
                    DateOfBirth = result.DateOfBirth,
                    Mobile = result.Mobile,
                    MobileVerified = result.MobileVerified,
                    IsNew = result.IsNew,
                    AbhaAccountId = account.AbhaAccountId
                };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmEnrollResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
