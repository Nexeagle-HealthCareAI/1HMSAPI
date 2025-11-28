using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorUpdateHandler : IRequestHandler<DoctorUpdateRequestModel, DoctorUpdateResponseModel>
    {
        private readonly AppDbContext _context;

        public DoctorUpdateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorUpdateResponseModel> Handle(DoctorUpdateRequestModel request, CancellationToken cancellationToken)
        {
            var updatedFields = new List<string>();
            var errors = new List<string>();
            var updateTime = DateTime.UtcNow;
            try
            {
                // Fetch user's hospital from HospitalUsers
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

                var current = await _context.Doctors
                    .Where(d => d.UserID == request.UserId)
                    .Select(d => new
                    {
                        d.DoctorID,
                        d.UserID,
                        d.LicenseNumber,
                        d.Qualification,
                        d.ExperienceYears,
                        d.MedicalCouncil,
                        d.RegistrationYear,
                        d.Bio,
                        d.PrimaryDepartmentID,
                        HospitalId = userWithHospital
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (current == null)
                {
                    return new DoctorUpdateResponseModel
                    {
                        Success = false,
                        Message = "Doctor not found.",
                        Errors = new List<string> { "Invalid User ID or Doctor profile does not exist" }
                    };
                }

                var doctorId = current.DoctorID;

                var doctor = new Doctor { DoctorID = doctorId };
                _context.Doctors.Attach(doctor);

                if (!string.IsNullOrEmpty(request.LicenseNumber) && request.LicenseNumber != current.LicenseNumber)
                {
                    doctor.LicenseNumber = request.LicenseNumber;
                    updatedFields.Add("LicenseNumber");
                    _context.Entry(doctor).Property(d => d.LicenseNumber).IsModified = true;
                }

                string? joinedQualification = null;
                if (request.Qualification != null)
                {
                    joinedQualification = JoinQualifications(request.Qualification);
                    if (joinedQualification != current.Qualification)
                    {
                        doctor.Qualification = joinedQualification;
                        updatedFields.Add("Qualification");
                        _context.Entry(doctor).Property(d => d.Qualification).IsModified = true;
                    }
                }

                if (request.ExperienceYears.HasValue && request.ExperienceYears != current.ExperienceYears)
                {
                    doctor.ExperienceYears = request.ExperienceYears;
                    updatedFields.Add("ExperienceYears");
                    _context.Entry(doctor).Property(d => d.ExperienceYears).IsModified = true;
                }

                if (!string.IsNullOrEmpty(request.MedicalCouncil) && request.MedicalCouncil != current.MedicalCouncil)
                {
                    doctor.MedicalCouncil = request.MedicalCouncil;
                    updatedFields.Add("MedicalCouncil");
                    _context.Entry(doctor).Property(d => d.MedicalCouncil).IsModified = true;
                }

                if (request.RegistrationYear.HasValue && request.RegistrationYear != current.RegistrationYear)
                {
                    doctor.RegistrationYear = request.RegistrationYear;
                    updatedFields.Add("RegistrationYear");
                    _context.Entry(doctor).Property(d => d.RegistrationYear).IsModified = true;
                }

                if (!string.IsNullOrEmpty(request.Bio) && request.Bio != current.Bio)
                {
                    doctor.Bio = request.Bio;
                    updatedFields.Add("Bio");
                    _context.Entry(doctor).Property(d => d.Bio).IsModified = true;
                }

                if (!string.IsNullOrEmpty(request.PrimaryDepartment))
                {
                    var primaryDepartmentId = await _context.Departments
                        .Where(d => d.Name.ToLower() == request.PrimaryDepartment.ToLower())
                        .Select(d => d.DepartmentID)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (primaryDepartmentId != Guid.Empty && primaryDepartmentId != current.PrimaryDepartmentID)
                    {
                        doctor.PrimaryDepartmentID = primaryDepartmentId;
                        updatedFields.Add("PrimaryDepartment");
                        _context.Entry(doctor).Property(d => d.PrimaryDepartmentID).IsModified = true;
                    }
                    else if (primaryDepartmentId == Guid.Empty)
                    {
                        errors.Add($"Primary department '{request.PrimaryDepartment}' not found.");
                    }
                }

                Guid? departmentIdForSpecializations = null;
                if (!string.IsNullOrEmpty(request.Department))
                {
                    var departmentId = await _context.Departments
                        .Where(d => d.Name.ToLower() == request.Department.ToLower())
                        .Select(d => d.DepartmentID)
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    if (departmentId != Guid.Empty)
                    {

                        var existingDepts = await _context.DoctorDepartments
                            .Where(dd => dd.DoctorID == doctorId)
                            .ToListAsync(cancellationToken);
                        if (existingDepts.Count > 0)
                            _context.DoctorDepartments.RemoveRange(existingDepts);

                        var doctorDepartment = new DoctorDepartment
                        {
                            DoctorDepartmentID = Guid.NewGuid(),
                            HospitalId= userWithHospital.HospitalId,
                            DoctorID = doctorId,
                            DepartmentID = departmentId,
                            AssignedAt = updateTime
                        };
                        _context.DoctorDepartments.Add(doctorDepartment);
                        departmentIdForSpecializations = departmentId;
                        updatedFields.Add("Department");
                        
                        if(request.HospitalDepartmentMappingId != Guid.Empty)
                        {
                            var existingMapping = await _context.HospitalDepartmentMappings
                                .Where(x => x.MappingID == request.HospitalDepartmentMappingId).FirstOrDefaultAsync(cancellationToken);
                            if(existingMapping is not null)
                            {
                                if (departmentId != Guid.Empty)
                                {
                                    existingMapping.DepartmentID = departmentId;
                                }
                            }
                        }
                    }
                    else
                    {
                        errors.Add($"Department '{request.Department}' not found.");
                    }
                }

                if (request.Specializations != null)
                {
                    var existingSpecializations = await _context.DoctorSpecializations
                        .Where(ds => ds.DoctorID == doctorId)
                        .ToListAsync(cancellationToken);
                    if (existingSpecializations.Count > 0)
                        _context.DoctorSpecializations.RemoveRange(existingSpecializations);

                    if (request.Specializations.Count > 0)
                    {
                        if (departmentIdForSpecializations == null)
                        {
                            var existingDept = await _context.DoctorDepartments
                                .Where(dd => dd.DoctorID == doctorId)
                                .FirstOrDefaultAsync(cancellationToken);
                            if (existingDept != null)
                            {
                                departmentIdForSpecializations = existingDept.DepartmentID;
                            }
                        }

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
                                            && s.Name.ToLower() == specName.ToLower())
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
                                        CreatedAt = updateTime,
                                        IsActive = true
                                    };
                                    _context.Specializations.Add(specialization);
                                }

                                var ds = new DoctorSpecialization
                                {
                                    DoctorSpecializationID = Guid.NewGuid(),
                                    DoctorID = doctorId,
                                    HospitalId=userWithHospital.HospitalId,
                                    SpecializationID = specialization.SpecializationID,
                                    AssignedAt = updateTime
                                };
                                _context.DoctorSpecializations.Add(ds);
                            }
                            updatedFields.Add("DoctorSpecializations");
                        }
                    }
                }

                if (errors.Count != 0)
                {
                    return new DoctorUpdateResponseModel
                    {
                        Success = false,
                        Message = "Update failed due to validation errors.",
                        Errors = errors,
                        UserId = request.UserId,
                        DoctorId = doctor.DoctorID
                    };
                }

                var finalDoctorForCalc = new Doctor
                {
                    LicenseNumber = !string.IsNullOrEmpty(request.LicenseNumber) ? request.LicenseNumber : current.LicenseNumber,
                    Qualification = request.Qualification != null ? (joinedQualification ?? current.Qualification) : current.Qualification,
                    ExperienceYears = request.ExperienceYears.HasValue ? request.ExperienceYears : current.ExperienceYears,
                    MedicalCouncil = !string.IsNullOrEmpty(request.MedicalCouncil) ? request.MedicalCouncil : current.MedicalCouncil,
                    RegistrationYear = request.RegistrationYear.HasValue ? request.RegistrationYear : current.RegistrationYear,
                    Bio = !string.IsNullOrEmpty(request.Bio) ? request.Bio : current.Bio,
                    PrimaryDepartmentID = doctor.PrimaryDepartmentID ?? current.PrimaryDepartmentID
                };

                var hasDepartmentNow = await _context.DoctorDepartments.AnyAsync(dd => dd.DoctorID == doctorId, cancellationToken);
                var hasSpecializationsNow = await _context.DoctorSpecializations.AnyAsync(ds => ds.DoctorID == doctorId, cancellationToken);

                doctor.ProfileCompletionPercent = CalculateProfileCompletion(finalDoctorForCalc, hasDepartmentNow, hasSpecializationsNow);
                _context.Entry(doctor).Property(d => d.ProfileCompletionPercent).IsModified = true;


                await _context.SaveChangesAsync(cancellationToken);

                return new DoctorUpdateResponseModel
                {
                    Success = true,
                    Message = updatedFields.Count != 0 ? "Doctor profile updated successfully." : "No changes were made.",
                    DoctorId = doctor.DoctorID,
                    UserId = request.UserId,
                    UpdatedAt = updateTime,
                    UpdatedFields = updatedFields,
                    Errors = null
                };
            }
            catch (Exception ex)
            {
                return new DoctorUpdateResponseModel
                {
                    Success = false,
                    Message = "An error occurred while updating the doctor profile.",
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
            if (hasDepartment) score += 10;

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
    }
}
