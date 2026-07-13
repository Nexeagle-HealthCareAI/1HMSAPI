using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreatePublicApiClientHandler : IRequestHandler<CreatePublicApiClientRequestModel, CreatePublicApiClientResponseModel>
    {
        private readonly AppDbContext _context;

        public CreatePublicApiClientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreatePublicApiClientResponseModel> Handle(CreatePublicApiClientRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new CreatePublicApiClientResponseModel { Success = false, Message = "hospitalId is required." };

            var rawKey = ApiKeyHasher.GenerateRawKey();
            var client = new PublicApiClient
            {
                ApiClientId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                ClientName = request.ClientName,
                ApiKeyHash = ApiKeyHasher.Hash(rawKey),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.PublicApiClient.Add(client);
            await _context.SaveChangesAsync(cancellationToken);

            return new CreatePublicApiClientResponseModel
            {
                Success = true,
                Message = "API key created. Copy it now — it will not be shown again.",
                ApiClientId = client.ApiClientId,
                ApiKey = rawKey,
            };
        }
    }
}
