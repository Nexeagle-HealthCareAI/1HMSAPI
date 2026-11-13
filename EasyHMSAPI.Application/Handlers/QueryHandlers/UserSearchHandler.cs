using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class UserSearchHandler : IRequestHandler<UserSearchRequestModel, UserSearchResponseModel?>
    {
        private readonly AppDbContext _context;
        public UserSearchHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserSearchResponseModel?> Handle(UserSearchRequestModel request, CancellationToken cancellationToken)
        {
            if (request.UserId == null)
                return null;

            // Self, or an admin_panel holder who shares a hospital with the target. CallerUserId is
            // stamped by the controller from the JWT; a missing caller (or a stranger) gets Forbidden.
            if (!await Common.CallerGuards.CanAccessUserAsync(_context, request.CallerUserId, request.UserId.Value, cancellationToken))
                return new UserSearchResponseModel { Forbidden = true };

            var user = await _context.Users
                .Include(u => u.UserAuths)
                .Include(u => u.UserProfiles)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.Hospital)
                .FirstOrDefaultAsync(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);

            if (user == null)
                return null;

            var userAuth = user.UserAuths.FirstOrDefault();
            var userProfile = user.UserProfiles.FirstOrDefault();
            
            var response = new UserSearchResponseModel
            {

                UserId = user.UserID,
                MobileNumber = user.MobileNumber,
                Email = user.Email,
                UserStatusId = user.UserStatusId,
                CreatedAt = user.CreatedAt,

                // Login-security details (lockout, last IP) are the user's own business; an admin
                // viewing a member does not need them.
                UserAuth = userAuth != null && request.CallerUserId == request.UserId ? new UserAuthInfo
                {
                    UserAuthId = userAuth.UserAuthID,
                    LoginMethod = userAuth.LoginMethod,
                    FailedLoginAttempts = userAuth.FailedLoginAttempts,
                    IsLocked = userAuth.IsLocked,
                    LastLoginIP = userAuth.LastLoginIP,
                    LastLoginTime = userAuth.LastLoginTime,
                    PasswordSetAt = userAuth.PasswordSetAt,
                    CreatedAt = userAuth.CreatedAt
                } : null,

                UserProfile = userProfile != null ? new UserProfileInfo
                {
                    UserProfileId = userProfile.UserProfileID,
                    FullName = userProfile.FullName,
                    Gender = userProfile.Gender,
                    Language = userProfile.Language,
                    ProfilePictureURL = userProfile.ProfilePictureURL,
                    EmployeeID = userProfile.EmployeeID,
                    DateOfBirth = userProfile.DateOfBirth,
                    BloodGroup = userProfile.BloodGroup,
                    AddressLine1 = userProfile.AddressLine1,
                    AddressLine2 = userProfile.AddressLine2,
                    City = userProfile.City,
                    State = userProfile.State,
                    Country = userProfile.Country,
                    Pincode = userProfile.Pincode,
                    EmergencyContactName = userProfile.EmergencyContactName,
                    EmergencyContactNumber = userProfile.EmergencyContactNumber,
                    ProfileCompletionPercentage = userProfile.ProfileCompletionPercentage,
                    CreatedAt = userProfile.CreatedAt,
                    UpdatedAt = userProfile.UpdatedAt
                } : null,                
            };

            return response;
        }
    }
}