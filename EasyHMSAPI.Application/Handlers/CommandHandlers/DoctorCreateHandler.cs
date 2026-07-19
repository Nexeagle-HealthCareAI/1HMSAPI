using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorCreateHandler : IRequestHandler<DoctorCreateRequestModel, DoctorCreateResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISubscriptionLimitHelper _subscriptionLimitHelper;

        public DoctorCreateHandler(AppDbContext context, ISubscriptionLimitHelper subscriptionLimitHelper)
        {
            _context = context;
            _subscriptionLimitHelper = subscriptionLimitHelper;
        }

        public async Task<DoctorCreateResponseModel> Handle(DoctorCreateRequestModel request, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var createdAt = DateTime.UtcNow;
            try
            {
                var userWithHospital = await _context.Users
                    .Where(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked)
                    .Select(u => new
                    {
                        UserExists = true,
                        HospitalId = _context.HospitalUsers
                            .Where(hu => hu.UserID == u.UserID && hu.HospitalID != Guid.Empty)
                            .Select(hu => (Guid?)hu.HospitalID)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(cancellationToken) ?? new { UserExists = false, HospitalId = (Guid?)null };

                if (!userWithHospital.UserExists)
                {
                    return new DoctorCreateResponseModel
                    {
                        Success = false,
                        Message = "User not found.",
                        Errors = new List<string> { "Invalid User ID" }
                    };
                }

                var existingDoctor = await _context.Doctors.AnyAsync(d => d.UserID == request.UserId, cancellationToken);
                if (existingDoctor)
                {
                    return new DoctorCreateResponseModel
                    {
                        Success = false,
                        Message = "Doctor profile already exists for this user.",
                        Errors = ["Duplicate doctor profile"]
                    };
                }

                var targetHospitalId = request.HospitalId ?? userWithHospital.HospitalId;
                if (targetHospitalId.HasValue)
                {
                    var limitCheck = await _subscriptionLimitHelper.CanAddDoctorAsync(targetHospitalId.Value, cancellationToken);
                    if (!limitCheck.Allowed)
                    {
                        return new DoctorCreateResponseModel
                        {
                            Success = false,
                            Message = limitCheck.Reason,
                            Errors = new List<string> { limitCheck.Reason! },
                            UserId = request.UserId
                        };
                    }
                }

                Guid? primaryDepartmentId = null;
                if (!string.IsNullOrWhiteSpace(request.PrimaryDepartment))
                {
                    var primaryDeptName = request.PrimaryDepartment.Trim();
                    primaryDepartmentId = await _context.Departments
                        .Where(d => d.Name != null && d.Name.ToLower() == primaryDeptName.ToLower())
                        .Select(d => (Guid?)d.DepartmentID)
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    if (!primaryDepartmentId.HasValue || primaryDepartmentId.Value == Guid.Empty)
                    {
                        errors.Add($"Primary department '{primaryDeptName}' not found.");
                        primaryDepartmentId = null;
                    }
                }

                Guid? primaryMedicalSpecialityId = null;
                if (request.PrimaryMedicalSpecialityId.HasValue)
                {
                    var exists = await _context.MedicalSpecialities
                        .AnyAsync(s => s.SpecialityId == request.PrimaryMedicalSpecialityId.Value && s.IsActive, cancellationToken);
                    if (exists)
                        primaryMedicalSpecialityId = request.PrimaryMedicalSpecialityId.Value;
                    else
                        errors.Add("Selected primary speciality was not found.");
                }

                var doctorId = Guid.NewGuid();
                var doctor = new Doctor
                {
                    DoctorID = doctorId,
                    UserID = request.UserId,
                    LicenseNumber = request.LicenseNumber,
                    Qualification = JoinQualifications(request.Qualification),
                    ExperienceYears = request.ExperienceYears,
                    MedicalCouncil = request.MedicalCouncil,
                    RegistrationYear = request.RegistrationYear,
                    Bio = request.Bio,
                    LanguagesJson = JoinLanguages(request.Languages),
                    PublicContactEmail = string.IsNullOrWhiteSpace(request.PublicContactEmail) ? null : request.PublicContactEmail.Trim(),
                    PublicContactPhone = string.IsNullOrWhiteSpace(request.PublicContactPhone) ? null : request.PublicContactPhone.Trim(),
                    PrimaryDepartmentID = primaryDepartmentId,
                    PrimaryMedicalSpecialityId = primaryMedicalSpecialityId,
                    CreatedAt = createdAt,
                    HospitalId = request.HospitalId ?? userWithHospital.HospitalId
                };
                _context.Doctors.Add(doctor);

                Guid? departmentIdForSpecializations = null;
                if (!string.IsNullOrWhiteSpace(request.Department))
                {
                    var deptName = request.Department.Trim();
                    var departmentId = await _context.Departments
                        .Where(d => d.Name != null && d.Name.ToLower() == deptName.ToLower())
                        .Select(d => (Guid?)d.DepartmentID)
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    if (departmentId.HasValue && departmentId.Value != Guid.Empty)
                    {
                        var doctorDepartment = new DoctorDepartment
                        {
                            DoctorDepartmentID = Guid.NewGuid(),
                            DoctorID = doctorId,
                            DepartmentID = departmentId.Value,
                            AssignedAt = createdAt,
                            HospitalId = request.HospitalId ?? userWithHospital.HospitalId
                        };
                        _context.DoctorDepartments.Add(doctorDepartment);
                        departmentIdForSpecializations = departmentId;
                        
                        if (request.HospitalId.HasValue && request.HospitalId.Value != Guid.Empty)
                        {
                            var existingMapping = await _context.HospitalDepartmentMappings
                                .AnyAsync(m => m.HospitalID == request.HospitalId && 
                                             m.DepartmentID == departmentId, 
                                         cancellationToken);
                            
                            if (!existingMapping)
                            {
                                var hospitalDepartmentMapping = new HospitalDepartmentMapping
                                {
                                    MappingID = Guid.NewGuid(),
                                    HospitalID = request.HospitalId.Value,
                                    DepartmentID = departmentId.Value,
                                    IsActive = true,
                                    MappedAt = createdAt
                                };
                                _context.HospitalDepartmentMappings.Add(hospitalDepartmentMapping);
                            }
                        }
                    }
                    else
                    {
                        errors.Add($"Department '{request.Department}' not found.");
                    }
                }

                if (request.Specializations != null && request.Specializations.Count > 0)
                {
                    if (departmentIdForSpecializations == null)
                    {
                        errors.Add("Department is required to assign or create specializations.");
                    }
                    else
                    {
                        var normalizedNames = request.Specializations
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        foreach (var specName in normalizedNames)
                        {

                            var matches = await _context.Specializations
                                .Where(s => s.DepartmentID == departmentIdForSpecializations
                                        && s.Name != null && specName != null && s.Name.ToLower() == specName.ToLower())
                                .ToListAsync(cancellationToken);


                            var specialization = matches.FirstOrDefault(s => s.HospitalID == null) ?? matches.FirstOrDefault();

                            if (specialization == null)
                            {

                                specialization = new Specialization
                                {
                                    SpecializationID = Guid.NewGuid(),
                                    DepartmentID = departmentIdForSpecializations.Value,
                                    HospitalID = null,
                                    Name = specName,
                                    Description = null,
                                    CreatedByUserID = request.UserId,
                                    CreatedAt = createdAt,
                                    IsActive = true
                                };
                                _context.Specializations.Add(specialization);
                            }

                            var ds = new DoctorSpecialization
                            {
                                DoctorSpecializationID = Guid.NewGuid(),
                                DoctorID = doctorId,
                                SpecializationID = specialization.SpecializationID,
                                AssignedAt = createdAt,
                                // Set hospitalId if provided
                                HospitalId = request.HospitalId ?? userWithHospital.HospitalId
                            };
                            _context.DoctorSpecializations.Add(ds);
                        }
                    }
                }

                doctor.ProfileCompletionPercent = CalculateProfileCompletion(doctor, hasDepartment: departmentIdForSpecializations != null, hasSpecializations: request.Specializations != null && request.Specializations.Count > 0);

                SaveDefaultPrescriptionSettings(doctorId, request.HospitalId, request.UserId, createdAt);
                SaveDefaultDoctorSectionPreference(request.HospitalId ?? userWithHospital.HospitalId ?? Guid.Empty, doctorId);


                if (errors.Count > 0)
                {
                    return new DoctorCreateResponseModel
                    {
                        Success = false,
                        Message = "Doctor creation failed due to validation errors.",
                        Errors = errors,
                        UserId = request.UserId
                    };
                }

                await _context.SaveChangesAsync(cancellationToken);
                
                return new DoctorCreateResponseModel
                {
                    Success = true,
                    Message = "Doctor profile created successfully.",
                    DoctorId = doctorId,
                    UserId = request.UserId,
                    CreatedAt = createdAt,
                    Errors = null
                };
            }
            catch (Exception ex)
            {
                return new DoctorCreateResponseModel
                {
                    Success = false,
                    Message = "An error occurred while creating the doctor profile.",
                    Errors = new List<string> { ex.Message },
                    UserId = request.UserId
                };
            }
        }

        private static int CalculateProfileCompletion(Doctor d, bool hasDepartment, bool hasSpecializations)
        {
            int score = 0;
            int currentYear = DateTime.UtcNow.Year;
            
            if (!string.IsNullOrWhiteSpace(d.LicenseNumber?.Trim())) score += 30;
            if (!string.IsNullOrWhiteSpace(d.Qualification?.Trim()) && d.Qualification.Trim().Length >= 2) score += 15;
            if (d.ExperienceYears.HasValue && d.ExperienceYears >= 0 && d.ExperienceYears <= 60) score += 15;
            if (!string.IsNullOrWhiteSpace(d.MedicalCouncil?.Trim())) score += 10;
            if (d.RegistrationYear.HasValue && d.RegistrationYear >= 1950 && d.RegistrationYear <= currentYear) score += 10;
            if (!string.IsNullOrWhiteSpace(d.Bio)) score += 10;
            if(hasDepartment) score += 10;

            if (score < 0) score = 0;
            if (score > 100) score = 100;

            return score;
        }

        private static string? JoinQualifications(List<string>? qualifications)
        {
            if (qualifications == null || qualifications.Count == 0) return null;
            var parts = qualifications
                .Select(q => (q ?? string.Empty).Trim())
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToList();
            if (parts.Count == 0) return null;
            return string.Join(", ", parts);
        }

        private static string? JoinLanguages(List<string>? languages)
        {
            if (languages == null) return null;
            var parts = languages
                .Select(l => (l ?? string.Empty).Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return parts.Count == 0 ? null : JsonSerializer.Serialize(parts);
        }

        private void SaveDefaultPrescriptionSettings(Guid doctorId, Guid? hospitalId, Guid userId, DateTime createdAt)
        {
            PrescriptionSetting newSettings = new PrescriptionSetting
            {
                PrescriptionSettingId = Guid.NewGuid(),
                HospitalId = doctorId,
                DoctorId = doctorId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedByUserId = userId
            };
            _context.PrescriptionSettings.Add(newSettings);
        }

        private void SaveDefaultDoctorSectionPreference(Guid hospitalId, Guid doctorId)
        {
            var preference = new DoctorSectionPreference
            {
                PreferenceId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctorId,
                Vitals = true,
                ChiefComplaint = true,
                History = true,
                Comorbidity = true,
                Examination = true,
                Diagnosis = true,
                Investigations = true,
                Procedures = true,
                Medications = true,
                PrivateNotes = true,
                CertificatesAndNotes = true,
                Immunizations = true,
                FollowUpAndReferral = true,
                NonPharmacologicalAdvice = true,
                Attachments = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _context.DoctorSectionPreferences.Add(preference);
            
        }
    }
}
