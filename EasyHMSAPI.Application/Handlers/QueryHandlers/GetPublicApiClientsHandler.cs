using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPublicApiClientsHandler : IRequestHandler<GetPublicApiClientsRequestModel, GetPublicApiClientsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicApiClientsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicApiClientsResponseModel> Handle(GetPublicApiClientsRequestModel request, CancellationToken cancellationToken)
        {
            var clients = await _context.PublicApiClient
                .Where(c => c.HospitalId == request.HospitalId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new PublicApiClientSummaryModel
                {
                    ApiClientId = c.ApiClientId,
                    ClientName = c.ClientName,
                    IsActive = c.IsActive,
                    LastUsedAt = c.LastUsedAt,
                    CreatedAt = c.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            return new GetPublicApiClientsResponseModel { Success = true, Clients = clients };
        }
    }
}
