using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetProfilePictureHandler : IRequestHandler<GetProfilePictureRequestModel, GetProfilePictureResponseModel>
    {
        private readonly string _containerName;
        private readonly IBlobStorageService _blobService;
        private readonly AppDbContext _context;

        public GetProfilePictureHandler(IConfiguration configuration, IBlobStorageService blobService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:ProfilePhotosContainer"] ?? string.Empty;
            _blobService = blobService;
            _context = context;
        }

        public async Task<GetProfilePictureResponseModel> Handle(GetProfilePictureRequestModel request, CancellationToken cancellationToken)
        {
            var userExists = await _context.Users.Where(x => x.UserID == request.UserId && x.UserStatusId != (int)UserStatusEnum.Revoked).Select(x => x.UserID).FirstOrDefaultAsync(cancellationToken);
            GetProfilePictureResponseModel response = new();

            if (userExists == Guid.Empty)
            {
                response.Success = false;
                response.ProfilePictureUrl = string.Empty;
            }
            else
            {
                var url = await _blobService.GetUrlAsync(request.UserId, _containerName, cancellationToken);

                response.Success = true;
                response.ProfilePictureUrl = url as string;
            }

            return response;
        }
    }
}