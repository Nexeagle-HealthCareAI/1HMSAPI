using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorCreateHandler : IRequestHandler<DoctorCreateRequestModel, DoctorCreateResponseModel>
    {
        private readonly AppDbContext _context;

        public DoctorCreateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorCreateResponseModel> Handle(DoctorCreateRequestModel request, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var createdAt = DateTime.UtcNow;
            try
            {
                var userWithHospital = await _context.Users
                    .Where(u => u.UserID == request.UserId)
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
                    PrimaryDepartmentID = primaryDepartmentId,
                    CreatedAt = createdAt
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
                            AssignedAt = createdAt
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
                                AssignedAt = createdAt
                            };
                            _context.DoctorSpecializations.Add(ds);
                        }
                    }
                }

                doctor.ProfileCompletionPercent = CalculateProfileCompletion(doctor, hasDepartment: departmentIdForSpecializations != null, hasSpecializations: request.Specializations != null && request.Specializations.Count > 0);

                SaveDefaultPrescriptionSettings(doctorId, createdAt);
                SaveDefaultDoctorSectionPreference(request.HospitalId ?? Guid.Empty, doctorId);


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

        private void SaveDefaultPrescriptionSettings(Guid doctorId, DateTime createdAt)
        {
            var defaultSettings = new PrescriptionSettingsDataModel
            {
                PageLayout = new PageLayoutDataModel
                {
                    Orientation = "portrait",
                    Margin = new MarginDataModel
                    {
                        Top = 15,
                        Right = 15,
                        Bottom = 15,
                        Left = 15
                    }
                },
                UseLetterhead = true,
                LetterheadSettings = new LetterheadSettingsDataModel { HeaderHeight = 30, FooterHeight = 20 },
                UseHeaderSettings = false,
                HeaderSettings = new HeaderSettingsDataModel
                {
                    Height = 0,
                    Width = 0,
                    ShowImage = false,
                    ShowOnAllPages = false
                },
                UseFooterSettings = false,
                FooterSettings = new FooterSettingsDataModel
                {
                    Height = 0,
                    Width = 0,
                    ShowImage = false,
                    ShowOnAllPages = false
                },
                UseDoctorSetting = false,
                DoctorSetting = new DoctorSettingDataModel
                {
                    ShowSignature = false,
                    SignatureHeight = 0,
                    SignatureWidth = 0,
                    DoctorName = string.Empty
                }
            };
            var settingsEntity = new PrescriptionSetting
            {
                PrescriptionSettingId = Guid.NewGuid(),
                DoctorId = doctorId,
                PageLayoutJson = JsonSerializer.Serialize(defaultSettings.PageLayout),
                LetterheadSettingsJson = JsonSerializer.Serialize(new
                {
                    defaultSettings.UseLetterhead,
                    defaultSettings.LetterheadSettings.HeaderHeight,
                    defaultSettings.LetterheadSettings.FooterHeight
                }),
                HeaderSettingsJson = JsonSerializer.Serialize(new
                {
                    defaultSettings.UseHeaderSettings,
                    defaultSettings.HeaderSettings.Height,
                    defaultSettings.HeaderSettings.Width,
                    defaultSettings.HeaderSettings.ShowImage,
                    defaultSettings.HeaderSettings.ShowOnAllPages
                }),
                FooterSettingsJson = JsonSerializer.Serialize(new
                {
                    defaultSettings.UseFooterSettings,
                    defaultSettings.FooterSettings.Height,
                    defaultSettings.FooterSettings.Width,
                    defaultSettings.FooterSettings.ShowImage,
                    defaultSettings.FooterSettings.ShowOnAllPages,
                    defaultSettings.DoctorSetting.ShowSignature,
                    defaultSettings.DoctorSetting.SignatureHeight,
                    defaultSettings.DoctorSetting.SignatureWidth,
                    defaultSettings.DoctorSetting.DoctorName
                }),
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            };

            _context.PrescriptionSettings.Add(settingsEntity);
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
