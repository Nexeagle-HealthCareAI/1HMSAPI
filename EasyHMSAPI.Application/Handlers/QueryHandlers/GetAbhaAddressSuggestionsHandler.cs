using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAbhaAddressSuggestionsHandler : IRequestHandler<GetAbhaAddressSuggestionsRequestModel, AbdmAddressSuggestionsResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public GetAbhaAddressSuggestionsHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmAddressSuggestionsResponseModel> Handle(GetAbhaAddressSuggestionsRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TxnId))
                return new AbdmAddressSuggestionsResponseModel { Success = false, Message = "Transaction is required." };

            try
            {
                var result = await _abha.GetAbhaAddressSuggestionsAsync(request.TxnId, cancellationToken);
                return new AbdmAddressSuggestionsResponseModel { Success = true, TxnId = result.TxnId, Suggestions = result.Suggestions };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmAddressSuggestionsResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
