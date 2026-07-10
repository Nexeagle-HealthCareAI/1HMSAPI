using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RevokePublicApiClientHandler : IRequestHandler<RevokePublicApiClientRequestModel, RevokePublicApiClientResponseModel>
    {
        private readonly AppDbContext _context;

        public RevokePublicApiClientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RevokePublicApiClientResponseModel> Handle(RevokePublicApiClientRequestModel request, CancellationToken cancellationToken)
        {
            var client = await _context.PublicApiClient
                .FirstOrDefaultAsync(c => c.ApiClientId == request.ApiClientId && c.HospitalId == request.HospitalId, cancellationToken);

            if (client == null)
                return new RevokePublicApiClientResponseModel { Success = false, Message = "API key not found." };

            client.IsActive = false;
            client.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return new RevokePublicApiClientResponseModel { Success = true, Message = "API key revoked." };
        }
    }
}
