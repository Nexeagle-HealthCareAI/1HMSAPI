using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Threading;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UserRegistrationHandler : IRequestHandler<UserRegistrationRequestModel, UserRegistrationResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IJwtAuthService _jwtAuthService;

        public UserRegistrationHandler(AppDbContext context, IConfiguration configuration, IJwtAuthService jwtAuthService)
        {
            _context = context;
            _jwtAuthService = jwtAuthService;
        }

        public async Task<UserRegistrationResponseModel> Handle(UserRegistrationRequestModel request, CancellationToken cancellationToken)
        {
            UserRegistrationResponseModel response = new()  ;

            if (string.IsNullOrWhiteSpace(request.MobileNumber) || string.IsNullOrWhiteSpace(request.Roles))
            {
                response.Success = false;
                response.Message = "Mobile number and roles are required.";
            }
            else
            {
                var existingUser = await _context.Users
                    .Where(x => x.MobileNumber == request.MobileNumber)
                    .Select(x => new{ x.MobileNumber, x.UserStatusId })
                    .FirstOrDefaultAsync(cancellationToken);

                if(existingUser != null)
                {
                    if(existingUser.UserStatusId == (int)UserStatusEnum.Revoked)
                    {
                        response = await CreateNewUser(request, cancellationToken);
                    }
                    else
                    {
                        response.Success = false;
                        response.Message = "User with this mobile number already exists.";
                    }
                }
                else
                {
                    response = await CreateNewUser(request, cancellationToken);
                }
            }

            return response;
        }

        public async Task<UserRegistrationResponseModel> CreateNewUser(UserRegistrationRequestModel request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(request.Roles))
            {
                var roleId = await _context.Roles.Where(x => x.RoleName.ToLower() == request.Roles.ToLower())
                    .Select(x => x.RoleID)
                    .FirstOrDefaultAsync(cancellationToken);
                var currentDateTime = DateTime.Now;

                if (roleId != Guid.Empty)
                {
                    var userId = Guid.NewGuid();
                    List<Claim> claims = new()
                    {
                        new Claim(ClaimTypes.MobilePhone, request.MobileNumber ?? string.Empty),
                        new Claim("userId", userId.ToString()),
                        new Claim("roles", request.Roles),
                    };
                    var accessToken = _jwtAuthService.GenerateJwtToken(claims);

                    var newUser = new User
                    {
                        UserID = userId,
                        MobileNumber = request.MobileNumber ?? string.Empty,
                        UserStatusId = (int)UserStatusEnum.Inactive,
                        CreatedAt = currentDateTime
                    };
                    _context.Users.Add(newUser);

                    var newAuthUser = new UserAuth
                    {
                        UserAuthID = Guid.NewGuid(),
                        UserID = userId,
                        UserStatusId = (int)UserStatusEnum.Inactive,
                        IsLocked = true
                    };
                    _context.UserAuths.Add(newAuthUser);

                    var newUserRole = new UserRole
                    {
                        UserID = userId,
                        RoleID = roleId
                    };
                    _context.UserRoles.Add(newUserRole);

                    string employeePrefix = "EMP";
                    string nextEmployeeId = await GenerateNextEmployeeIdAsync(employeePrefix, cancellationToken);

                    var newUserProfile = new UserProfile
                    {
                        UserProfileID = Guid.NewGuid(),
                        UserID = userId,
                        UserStatusId = (int)UserStatusEnum.Inactive,
                        FullName = request.FullName ?? string.Empty,
                        Language = "en-US",
                        EmployeeID = nextEmployeeId,
                        CreatedAt = currentDateTime,
                    };
                    newUserProfile.ProfileCompletionPercentage = CalculateUserProfileCompletion(newUserProfile);
                    _context.UserProfiles.Add(newUserProfile);

                    var userHistory = new UserHistory
                    {
                        UserId = userId,
                        UserStatusId = (int)UserStatusEnum.Inactive,
                        UpdatedBy = userId,
                        UpdatedDate = currentDateTime
                    };
                    _context.UserHistories.Add(userHistory);

                    await _context.SaveChangesAsync(cancellationToken);

                    return new UserRegistrationResponseModel
                    {
                        Success = true,
                        Message = "User registration successful.",
                        UserId = userId,
                        AccessToken = accessToken
                    };
                }
                else
                {
                    return new UserRegistrationResponseModel
                    {
                        Success = false,
                        Message = "Invalid role specified."
                    };
                }
            }
            else
            {
                return new UserRegistrationResponseModel
                {
                    Success = false,
                    Message = "Roles cannot be empty."
                };
            }
        }

        private async Task<string> GenerateNextEmployeeIdAsync(string prefix, CancellationToken cancellationToken)
        {
            var maxExisting = await _context.UserProfiles
                .Where(u => u.EmployeeID != null && u.EmployeeID.StartsWith(prefix))
                .Select(u => u.EmployeeID!)
                .ToListAsync(cancellationToken);

            int maxNumber = 0;
            foreach (var eid in maxExisting)
            {
                var numberPart = eid.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int n))
                {
                    if (n > maxNumber) maxNumber = n;
                }
            }

            return prefix + (maxNumber + 1).ToString();
        }

        private static int CalculateUserProfileCompletion(UserProfile profile)
        {
            int score = 0;
            
            if (!string.IsNullOrWhiteSpace(profile.FullName?.Trim())) score += 10;
            if (!string.IsNullOrWhiteSpace(profile.ProfilePictureURL?.Trim())) score += 15;
            if (!string.IsNullOrWhiteSpace(profile.EmployeeID?.Trim())) score += 5;
            
            if (!string.IsNullOrWhiteSpace(profile.Gender?.Trim())) score += 5;
            if (!string.IsNullOrWhiteSpace(profile.Language?.Trim())) score += 5;
            if (profile.DateOfBirth.HasValue && profile.DateOfBirth.Value.Year >= 1900 && profile.DateOfBirth.Value <= DateTime.UtcNow) score += 7;
            
            var validBloodGroups = new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };
            if (!string.IsNullOrWhiteSpace(profile.BloodGroup?.Trim()) && validBloodGroups.Contains(profile.BloodGroup.Trim())) score += 5;
            
            if (!string.IsNullOrWhiteSpace(profile.AddressLine1?.Trim())) score += 8;
            if (!string.IsNullOrWhiteSpace(profile.City?.Trim())) score += 5;
            if (!string.IsNullOrWhiteSpace(profile.State?.Trim())) score += 5;
            if (!string.IsNullOrWhiteSpace(profile.Country?.Trim())) score += 3;
            
            var pincode = profile.Pincode?.Trim();
            if (!string.IsNullOrWhiteSpace(pincode) && pincode.Length >= 4 && pincode.Length <= 10)
            {
                if (profile.Country?.Trim().Equals("India", StringComparison.OrdinalIgnoreCase) == true && pincode.Length == 6)
                    score += 7;
                else if (pincode.Length >= 4 && pincode.Length <= 10)
                    score += 7;
            }
            
            if (!string.IsNullOrWhiteSpace(profile.EmergencyContactName?.Trim())) score += 10;
            if (!string.IsNullOrWhiteSpace(profile.EmergencyContactNumber?.Trim()) && profile.EmergencyContactNumber.Trim().Length >= 7) score += 10;
            
            return score;
        }
    }
}