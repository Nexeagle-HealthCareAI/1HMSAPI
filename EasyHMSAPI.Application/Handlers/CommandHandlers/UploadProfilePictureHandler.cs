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

            // Self, or an admin_panel holder at the supplied hospital acting on a doctor of that hospital
            // (Public Directory tile editor). HospitalId is client-supplied, so it only counts when the
            // caller really holds admin_panel there; otherwise only the user's own picture may change.
            var isSelf = request.CallerUserId != null && request.CallerUserId == request.UserId;
            var isHospitalAdmin = !isSelf && request.HospitalId.HasValue && request.CallerUserId.HasValue
                && await Common.CallerGuards.HasPermissionAtHospitalAsync(_context, request.CallerUserId.Value, request.HospitalId.Value, "admin_panel", cancellationToken);
            if (!isSelf && !isHospitalAdmin)
            {
                response.Success = false;
                response.Forbidden = true;
                response.ProfilePictureUrl = string.Empty;
                return response;
            }

            if (userExists == Guid.Empty)
            {
                response.Success = false;
                response.ProfilePictureUrl = string.Empty;
            }
            else if (request.HospitalId.HasValue && !await _context.DoctorDepartments
                .AnyAsync(dd => dd.HospitalId == request.HospitalId.Value && dd.Doctor.UserID == request.UserId, cancellationToken))
            {
                response.Success = false;
                response.ProfilePictureUrl = string.Empty;
            }
            else
            {
                var url = await _blobService.UploadAsync(request.UserId.ToString(), request.File, _containerName, cancellationToken);

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