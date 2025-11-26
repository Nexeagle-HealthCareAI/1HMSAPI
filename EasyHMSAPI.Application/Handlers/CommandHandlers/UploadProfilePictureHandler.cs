using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadImageCommandHandler : IRequestHandler<UploadProfilePictureRequestModel, UploadProfilePictureResponseModel>
    {
        private readonly string _containerName;
        private readonly IBlobStorageService _blobService;
        private readonly AppDbContext _context;

        public UploadImageCommandHandler(IConfiguration configuration, IBlobStorageService blobService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:ProfilePhotosContainer"] ?? string.Empty;
            _blobService = blobService;
            _context = context;
        }

        public async Task<UploadProfilePictureResponseModel> Handle(UploadProfilePictureRequestModel request, CancellationToken cancellationToken)
        {
            var userExists = await _context.Users.Where(x => x.UserID == request.UserId && x.UserStatusId != (int)UserStatusEnum.Revoked).Select(x => x.UserID).FirstOrDefaultAsync(cancellationToken);
            UploadProfilePictureResponseModel response = new();

            if (userExists == Guid.Empty)
            {
                response.Success = false;
                response.ProfilePictureUrl = string.Empty;
            }
            else
            {
                var url = await _blobService.UploadAsync(request.UserId, request.File, _containerName, cancellationToken);

                if(!string.IsNullOrEmpty(url))
                {
                    var user = await _context.UserProfiles.Where(x => x.UserID == request.UserId).FirstOrDefaultAsync(cancellationToken);

                    if (user != null)
                    {
                        user.ProfilePictureURL = url;
                        await _context.SaveChangesAsync();

                        response.Success = true;
                        response.ProfilePictureUrl = url;
                    }
                }
            }

            return response;
        }
    }
}