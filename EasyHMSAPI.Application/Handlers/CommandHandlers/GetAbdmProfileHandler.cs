using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GetAbdmProfileHandler : IRequestHandler<GetAbdmProfileRequestModel, AbdmProfileResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public GetAbdmProfileHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmProfileResponseModel> Handle(GetAbdmProfileRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionTxnId))
                return new AbdmProfileResponseModel { Success = false, Message = "Session is required." };

            try
            {
                var result = await _abha.GetProfileAsync(request.SessionTxnId, cancellationToken);
                return new AbdmProfileResponseModel
                {
                    Success = true,
                    TxnId = result.TxnId,
                    AbhaNumber = result.AbhaNumber,
                    AbhaAddress = result.AbhaAddress,
                    FullName = result.FullName,
                    Gender = result.Gender,
                    DateOfBirth = result.DateOfBirth,
                    Mobile = result.Mobile,
                    Email = result.Email,
                    ProfilePhoto = result.ProfilePhoto
                };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmProfileResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
