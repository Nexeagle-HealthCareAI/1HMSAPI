using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UserProfileUpdateHandler : IRequestHandler<UserProfileUpdateRequestModel, UserProfileUpdateResponseModel>
    {
        private readonly AppDbContext _context;
        public UserProfileUpdateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfileUpdateResponseModel> Handle(UserProfileUpdateRequestModel request, CancellationToken cancellationToken)
        {
            var updatedFields = new List<string>();
            var errors = new List<string>();
            var updateTime = DateTime.UtcNow;
            try
            {
                DateTime? parsedDob = ParseNullableDate(request.DateOfBirth);
                var user = await _context.Users
                    .Include(u => u.UserAuths)
                    .Include(u => u.UserProfiles)
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);

                if (user == null)
                {
                    return new UserProfileUpdateResponseModel
                    {
                        Success = false,
                        Message = "User not found.",
                        Errors = new List<string> { "Invalid User ID" }
                    };
                }


                if (!string.IsNullOrEmpty(request.MobileNumber) && request.MobileNumber != user.MobileNumber)
                {
                    user.MobileNumber = request.MobileNumber;
                    updatedFields.Add("MobileNumber");
                }
                if (!string.IsNullOrEmpty(request.Email))
                {
                    user.Email = request.Email;
                    updatedFields.Add("Email");
                }

                var userProfile = user.UserProfiles.FirstOrDefault();
                if (userProfile != null)
                {
                    var profileUpdated = false;


                    if (!string.IsNullOrEmpty(request.FullName) && request.FullName != userProfile.FullName)
                    {
                        userProfile.FullName = request.FullName;
                        updatedFields.Add("FullName");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.Gender) && request.Gender != userProfile.Gender)
                    {
                        userProfile.Gender = request.Gender;
                        updatedFields.Add("Gender");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.Language) && request.Language != userProfile.Language)
                    {
                        userProfile.Language = request.Language;
                        updatedFields.Add("Language");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.ProfilePictureURL) && request.ProfilePictureURL != userProfile.ProfilePictureURL)
                    {
                        userProfile.ProfilePictureURL = request.ProfilePictureURL;
                        updatedFields.Add("ProfilePictureURL");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.EmployeeID) && request.EmployeeID != userProfile.EmployeeID)
                    {
                        userProfile.EmployeeID = request.EmployeeID;
                        updatedFields.Add("EmployeeID");
                        profileUpdated = true;
                    }

                    if (parsedDob.HasValue && parsedDob != userProfile.DateOfBirth)
                    {
                        userProfile.DateOfBirth = parsedDob;
                        updatedFields.Add("DateOfBirth");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.BloodGroup) && request.BloodGroup != userProfile.BloodGroup)
                    {
                        userProfile.BloodGroup = request.BloodGroup;
                        updatedFields.Add("BloodGroup");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.AddressLine1) && request.AddressLine1 != userProfile.AddressLine1)
                    {
                        userProfile.AddressLine1 = request.AddressLine1;
                        updatedFields.Add("AddressLine1");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.AddressLine2) && request.AddressLine2 != userProfile.AddressLine2)
                    {
                        userProfile.AddressLine2 = request.AddressLine2;
                        updatedFields.Add("AddressLine2");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.City) && request.City != userProfile.City)
                    {
                        userProfile.City = request.City;
                        updatedFields.Add("City");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.State) && request.State != userProfile.State)
                    {
                        userProfile.State = request.State;
                        updatedFields.Add("State");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.Country) && request.Country != userProfile.Country)
                    {
                        userProfile.Country = request.Country;
                        updatedFields.Add("Country");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.Pincode) && request.Pincode != userProfile.Pincode)
                    {
                        userProfile.Pincode = request.Pincode;
                        updatedFields.Add("Pincode");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.EmergencyContactName) && request.EmergencyContactName != userProfile.EmergencyContactName)
                    {
                        userProfile.EmergencyContactName = request.EmergencyContactName;
                        updatedFields.Add("EmergencyContactName");
                        profileUpdated = true;
                    }

                    if (!string.IsNullOrEmpty(request.EmergencyContactNumber) && request.EmergencyContactNumber != userProfile.EmergencyContactNumber)
                    {
                        userProfile.EmergencyContactNumber = request.EmergencyContactNumber;
                        updatedFields.Add("EmergencyContactNumber");
                        profileUpdated = true;
                    }

                    if (profileUpdated)
                    {
                        userProfile.ProfileCompletionPercentage = CalculateUserProfileCompletion(userProfile);
                        userProfile.UpdatedAt = updateTime;
                    }
                }
                else if (HasProfileFields(request))
                {
                    userProfile = new UserProfile
                    {
                        UserProfileID = Guid.NewGuid(),
                        UserID = request.UserId,
                        DateOfBirth = parsedDob,
                        ProfileCompletionPercentage = 0,
                        CreatedAt = updateTime,
                        UpdatedAt = updateTime
                    };

                    if (!string.IsNullOrEmpty(request.FullName)) userProfile.FullName = request.FullName;
                    if (!string.IsNullOrEmpty(request.Gender)) userProfile.Gender = request.Gender;
                    if (!string.IsNullOrEmpty(request.Language)) userProfile.Language = request.Language;
                    if (!string.IsNullOrEmpty(request.ProfilePictureURL)) userProfile.ProfilePictureURL = request.ProfilePictureURL;
                    if (!string.IsNullOrEmpty(request.EmployeeID)) userProfile.EmployeeID = request.EmployeeID;
                    if (!string.IsNullOrEmpty(request.BloodGroup)) userProfile.BloodGroup = request.BloodGroup;
                    if (!string.IsNullOrEmpty(request.AddressLine1)) userProfile.AddressLine1 = request.AddressLine1;
                    if (!string.IsNullOrEmpty(request.AddressLine2)) userProfile.AddressLine2 = request.AddressLine2;
                    if (!string.IsNullOrEmpty(request.City)) userProfile.City = request.City;
                    if (!string.IsNullOrEmpty(request.State)) userProfile.State = request.State;
                    if (!string.IsNullOrEmpty(request.Country)) userProfile.Country = request.Country;
                    if (!string.IsNullOrEmpty(request.Pincode)) userProfile.Pincode = request.Pincode;
                    if (!string.IsNullOrEmpty(request.EmergencyContactName)) userProfile.EmergencyContactName = request.EmergencyContactName;
                    if (!string.IsNullOrEmpty(request.EmergencyContactNumber)) userProfile.EmergencyContactNumber = request.EmergencyContactNumber;

                    userProfile.ProfileCompletionPercentage = CalculateUserProfileCompletion(userProfile);

                    _context.UserProfiles.Add(userProfile);
                    updatedFields.Add("UserProfile Created");
                }


                if (errors.Count != 0)
                {
                    return new UserProfileUpdateResponseModel
                    {
                        Success = false,
                        Message = "User profile update failed due to validation errors.",
                        Errors = errors,
                        UserId = request.UserId
                    };
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new UserProfileUpdateResponseModel
                {
                    Success = true,
                    Message = updatedFields.Any() ? "User profile updated successfully." : "No changes were made.",
                    UserId = request.UserId,
                    UpdatedAt = updateTime,
                    UpdatedFields = updatedFields,
                    Errors = null
                };
            }
            catch (Exception ex)
            {
                return new UserProfileUpdateResponseModel
                {
                    Success = false,
                    Message = "An error occurred while updating the user profile.",
                    Errors = new List<string> { ex.Message },
                    UserId = request.UserId
                };
            }
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

        private static bool HasProfileFields(UserProfileUpdateRequestModel request)
        {
            return !string.IsNullOrEmpty(request.FullName) ||
                   !string.IsNullOrEmpty(request.Gender) ||
                   !string.IsNullOrEmpty(request.Language) ||
                   !string.IsNullOrEmpty(request.ProfilePictureURL) ||
                   !string.IsNullOrEmpty(request.EmployeeID) ||
                   ParseNullableDate(request.DateOfBirth).HasValue ||
                   !string.IsNullOrEmpty(request.BloodGroup) ||
                   !string.IsNullOrEmpty(request.AddressLine1) ||
                   !string.IsNullOrEmpty(request.AddressLine2) ||
                   !string.IsNullOrEmpty(request.City) ||
                   !string.IsNullOrEmpty(request.State) ||
                   !string.IsNullOrEmpty(request.Country) ||
                   !string.IsNullOrEmpty(request.Pincode) ||
                   !string.IsNullOrEmpty(request.EmergencyContactName) ||
                   !string.IsNullOrEmpty(request.EmergencyContactNumber);
        }

        private static DateTime? ParseNullableDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var dt)) return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt)) return dt;
            if (DateTime.TryParse(s, out dt)) return dt;
            return null;
        }
    }
}