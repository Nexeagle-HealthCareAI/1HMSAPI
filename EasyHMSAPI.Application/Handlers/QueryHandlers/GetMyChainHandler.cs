using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Returns the chain the caller owns (if any) and its member hospitals.</summary>
    public class GetMyChainHandler : IRequestHandler<GetMyChainRequestModel, GetMyChainResponseModel>
    {
        private readonly AppDbContext _context;

        public GetMyChainHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMyChainResponseModel> Handle(GetMyChainRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.UserId == Guid.Empty)
                    return new GetMyChainResponseModel { Success = false, Message = "UserId is required." };

                var chain = await _context.HospitalChains
                    .Where(c => c.OwnerUserId == request.UserId)
                    .OrderBy(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (chain == null)
                    return new GetMyChainResponseModel { Success = true, ChainId = null };

                var hospitals = await _context.Hospitals
                    .Where(h => h.ChainId == chain.ChainId)
                    .OrderBy(h => h.Name)
                    .Select(h => new ChainHospitalItem
                    {
                        HospitalId = h.HospitalID,
                        Name = h.Name,
                        City = h.City,
                        State = h.State,
                        IsActive = h.IsActive,
                    })
                    .ToListAsync(cancellationToken);

                return new GetMyChainResponseModel
                {
                    Success = true,
                    ChainId = chain.ChainId,
                    ChainName = chain.Name,
                    Hospitals = hospitals,
                };
            }
            catch (Exception)
            {
                return new GetMyChainResponseModel { Success = false, Message = "Error loading chain." };
            }
        }
    }
}
