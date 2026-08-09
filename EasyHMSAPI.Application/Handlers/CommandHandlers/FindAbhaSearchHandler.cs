using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class FindAbhaSearchHandler : IRequestHandler<FindAbhaSearchRequestModel, AbdmFindAbhaSearchResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public FindAbhaSearchHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmFindAbhaSearchResponseModel> Handle(FindAbhaSearchRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Value))
                return new AbdmFindAbhaSearchResponseModel { Success = false, Message = "Enter a mobile or Aadhaar number." };

            try
            {
                var result = await _abha.FindAbhaSearchAsync(request.Value, request.SearchBy, cancellationToken);
                if (result.Candidates.Count == 0)
                    return new AbdmFindAbhaSearchResponseModel { Success = false, Message = "No ABHA number found for this value." };

                return new AbdmFindAbhaSearchResponseModel
                {
                    Success = true,
                    TxnId = result.TxnId,
                    Candidates = result.Candidates.Select(c => new AbdmFindAbhaCandidateModel
                    {
                        Index = c.Index,
                        AbhaNumber = c.AbhaNumber,
                        Name = c.Name,
                        Gender = c.Gender
                    }).ToList()
                };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmFindAbhaSearchResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
