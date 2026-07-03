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
                // Chain onboarding: only the chain owner may add a hospital into their chain.
                if (request.ChainId.HasValue)
                {
                    var chain = await _context.HospitalChains
                        .FirstOrDefaultAsync(c => c.ChainId == request.ChainId.Value, cancellationToken);
                    if (chain == null)
                        return new HospitalRegisterResponseModel { Success = false, Message = "Chain not found.", HospitalId = null, HospitalUserId = null };
                    if (chain.OwnerUserId != request.UserId)
                        return new HospitalRegisterResponseModel { Success = false, Message = "You are not the owner of this chain.", HospitalId = null, HospitalUserId = null };
                }
                var hospitalId = Guid.NewGuid();
                var hospital = new Hospital
                {
                    ChainId = request.ChainId,
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
                    TimeZone = request.TimeZone ?? string.Empty,
                    GSTIN = request.GstIn ?? string.Empty,
                    PAN = request.PanNumber ?? string.Empty,
                    NABH_NABL = request.NabhNabl ?? string.Empty
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
                    // The owner's first hospital is primary; hospitals onboarded into a chain are not.
                    IsPrimary = !request.ChainId.HasValue,
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

                var invoiceSetting = new InvoicePrintSettings
                {
                    InvoicePrintId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = request.UserId
                };
                _context.InvoicePrintSettings.Add(invoiceSetting);

                var billingPolicy = new BillingPolicy
                {
                    BillingPolicyId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName
                };
                _context.BillingPolicy.Add(billingPolicy);

                // --- Add/Update UserRoles with hospitalId ---
                // Only for a first/standalone hospital. For a chain onboarding we must NOT move the
                // owner's existing role onto the new hospital (per-hospital roles arrive in a later phase).
                if (!request.ChainId.HasValue)
                {
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
                }
                // --- End UserRoles update ---

                // --- Initialize 14-Day Trial Subscription ---
                var trialSub = new HospitalSubscription
                {
                    HospitalSubscriptionId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    Status = "Trial",
                    TrialStartDate = DateTime.UtcNow,
                    TrialEndDate = DateTime.UtcNow.AddDays(14),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.HospitalSubscriptions.Add(trialSub);

                // --- Automatically Setup Doctor profile and Default Department if AdminDoctor/Doctor ---
                var userRolesList = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserID == request.UserId)
                    .ToListAsync(cancellationToken);

                bool isDoctorOrAdminDoctor = userRolesList.Any(ur => ur.Role != null && 
                    (ur.Role.RoleName == "Doctor" || ur.Role.RoleName == "AdminDoctor"));

                if (isDoctorOrAdminDoctor)
                {
                    // Create a default department
                    var defaultDeptId = Guid.NewGuid();
                    var defaultDept = new Department
                    {
                        DepartmentID = defaultDeptId,
                        Name = "General Medicine",
                        Description = "Default department created during hospital registration",
                        HospitalID = hospitalId,
                        IsActive = true,
                        CreatedByUserID = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Departments.Add(defaultDept);

                    // Create Doctor profile
                    var doctorId = Guid.NewGuid();
                    var doctor = new Doctor
                    {
                        DoctorID = doctorId,
                        UserID = request.UserId,
                        LicenseNumber = "PENDING", // Needs to be updated later
                        HospitalId = hospitalId,
                        CreatedAt = DateTime.UtcNow,
                        PrimaryDepartmentID = defaultDeptId
                    };
                    _context.Doctors.Add(doctor);

                    // Map doctor to department
                    var docDept = new DoctorDepartment
                    {
                        DoctorID = doctorId,
                        DepartmentID = defaultDeptId,
                        HospitalId = hospitalId
                    };
                    _context.DoctorDepartments.Add(docDept);
                }
                // --- End Doctor setup ---

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