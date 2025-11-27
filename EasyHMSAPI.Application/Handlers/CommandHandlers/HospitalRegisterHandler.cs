using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class HospitalRegisterHandler : IRequestHandler<HospitalRegisterRequestModel, HospitalRegisterResponseModel>
    {
        private readonly AppDbContext _context;
        public HospitalRegisterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HospitalRegisterResponseModel> Handle(HospitalRegisterRequestModel request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Where(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked)
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                return new HospitalRegisterResponseModel
                {
                    Success = false,
                    Message = "User not found.",
                    HospitalId = null,
                    HospitalUserId = null
                };
            }
            else
            {
                var hospitalId = Guid.NewGuid();
                var hospital = new Hospital
                {
                    HospitalID = hospitalId,
                    Name = request.Name ?? string.Empty,
                    Type = request.Type ?? string.Empty,
                    Email = request.Email ?? string.Empty,
                    Contact = request.Contact ?? string.Empty,
                    Location = request.Location ?? string.Empty,
                    City = request.City ?? string.Empty,
                    State = request.State ?? string.Empty,
                    Country = request.Country ?? string.Empty,
                    Pincode = request.Pincode ?? string.Empty,
                    RegistrationNumber = request.RegistrationNumber ?? string.Empty,
                    CreatedByUserID = request.UserId,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    TimeZone = request.TimeZone ?? string.Empty
                };
                _context.Hospitals.Add(hospital);

                var employeeId = await _context.UserProfiles
                    .Where(up => up.UserID == request.UserId)
                    .Select(up => up.EmployeeID)
                    .FirstOrDefaultAsync(cancellationToken);

                var hospitalUser = new HospitalUser
                {
                    HospitalUserID = Guid.NewGuid(),
                    HospitalID = hospitalId,
                    UserID = request.UserId,
                    EmployeeID = employeeId ?? string.Empty,
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.HospitalUsers.Add(hospitalUser);
                
                int isBasicInfoComplete = (!string.IsNullOrEmpty(hospital.Name) && !string.IsNullOrEmpty(hospital.Type)) ? 1 : 0;
                int isContactInfoComplete = (!string.IsNullOrEmpty(hospital.Contact) && !string.IsNullOrEmpty(hospital.Email)) ? 1 : 0;
                int isLocationInfoComplete = (!string.IsNullOrEmpty(hospital.Location) && !string.IsNullOrEmpty(hospital.City) && !string.IsNullOrEmpty(hospital.State) && !string.IsNullOrEmpty(hospital.Country) && !string.IsNullOrEmpty(hospital.Pincode)) ? 1 : 0;
                int totalCompletedSections = isBasicInfoComplete + isContactInfoComplete + isLocationInfoComplete;
                int profileCompletionPercent = (int)((totalCompletedSections / 3.0) * 100);

                var hospitalProfileStatus = new HospitalProfileStatus
                {
                    HospitalID = hospitalId,
                    IsBasicInfoComplete = isBasicInfoComplete == 1,
                    IsContactInfoComplete = isContactInfoComplete == 1,
                    IsLocationInfoComplete = isLocationInfoComplete == 1,
                    
                    ProfileCompletionPercent = profileCompletionPercent,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _context.HospitalProfileStatuses.Add(hospitalProfileStatus);

                // --- Add/Update UserRoles with hospitalId ---
                // Find the user's current role (if any)
                var userRole = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .FirstOrDefaultAsync(ur => ur.UserID == request.UserId, cancellationToken);
                if (userRole != null && userRole.Role != null)
                {
                    // If the role is not already associated with a hospital, associate it
                    if (userRole.Role.HospitalID == null || userRole.Role.HospitalID == Guid.Empty)
                    {
                        userRole.Role.HospitalID = hospitalId;
                    }
                }
                // If no user role exists, you may want to assign a default role here (optional)
                // --- End UserRoles update ---

                await _context.SaveChangesAsync(cancellationToken);

                return new HospitalRegisterResponseModel
                {
                    Success = true,
                    Message = "Hospital registered successfully.",
                    HospitalId = hospitalId,
                    HospitalUserId = hospitalUser.HospitalUserID
                };
            }
        }
    }
}