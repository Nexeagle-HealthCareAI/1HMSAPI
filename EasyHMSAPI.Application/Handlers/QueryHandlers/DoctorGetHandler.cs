using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Data.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorGetHandler : IRequestHandler<DoctorGetRequestModel, DoctorGetResponseModel?>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobService;
        private readonly string _containerName;

        public DoctorGetHandler(AppDbContext context, IBlobStorageService blobService, IConfiguration configuration)
        {
            _context = context;
            _blobService = blobService;
            _containerName = configuration["BlobStorage:ProfilePhotosContainer"] ?? string.Empty;
        }

        public async Task<DoctorGetResponseModel?> Handle(DoctorGetRequestModel request, CancellationToken cancellationToken)
        {

            var temp = await (from d in _context.Doctors
                              join u in _context.Users on d.UserID equals u.UserID
                              where d.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked
                              select new
                              {
                                  DoctorId = d.DoctorID,
                                  UserId = d.UserID,
                                  d.LicenseNumber,
                                  Qualifications = d.Qualification,
                                  d.ExperienceYears,
                                  d.MedicalCouncil,
                                  d.RegistrationYear,
                                  d.Bio,
                                  d.LanguagesJson,
                                  d.PublicContactEmail,
                                  d.PublicContactPhone,
                                  d.IsPubliclyListed,
                                  d.PrimaryDepartmentID,
                                  PrimaryDepartmentName = d.PrimaryDepartment != null ? d.PrimaryDepartment.Name : null,
                                  d.PrimaryMedicalSpecialityId,
                                  PrimaryMedicalSpecialityName = d.PrimaryMedicalSpeciality != null ? d.PrimaryMedicalSpeciality.Name : null,
                                  PrimaryMedicalSpecialityPatientFacingName = d.PrimaryMedicalSpeciality != null ? d.PrimaryMedicalSpeciality.PatientFacingName : null,
                                  d.CreatedAt,
                                  ProfileCompletionPercentage = d.ProfileCompletionPercent ?? 0,

                                  DoctorDepartments = d.DoctorDepartments.Select(dd => new
                                  {
                                      DoctorDepartmentId = dd.DoctorDepartmentID,
                                      DepartmentId = dd.DepartmentID,
                                      DepartmentName = dd.Department.Name,
                                      DepartmentDescription = dd.Department.Description,
                                      AssignedAt = dd.AssignedAt
                                  }).ToList(),

                                  DoctorSpecializations = d.DoctorSpecializations.Select(ds => new DoctorSpecializationInfo
                                  {
                                      DoctorSpecializationId = ds.DoctorSpecializationID,
                                      SpecializationId = ds.SpecializationID,
                                      SpecializationName = ds.Specialization.Name,
                                      SpecializationDescription = ds.Specialization.Description,
                                      AssignedAt = ds.AssignedAt
                                  }).ToList()
                              })
                .FirstOrDefaultAsync(cancellationToken);

            if (temp == null) return null;

            var departmentIds = temp.DoctorDepartments.Select(dd => dd.DepartmentId).ToList();
            var hospitalDepartmentMappings = await _context.HospitalDepartmentMappings
                .Where(hdm => departmentIds.Contains(hdm.DepartmentID))
                .ToListAsync(cancellationToken);

            var doctorDepartments = temp.DoctorDepartments.Select(dd => new DoctorDepartmentInfo
            {
                DoctorDepartmentId = dd.DoctorDepartmentId,
                DepartmentId = dd.DepartmentId,
                DepartmentName = dd.DepartmentName,
                DepartmentDescription = dd.DepartmentDescription,
                AssignedAt = dd.AssignedAt,
                HospitalDepartmentMappingId = hospitalDepartmentMappings.FirstOrDefault(hdm => hdm.DepartmentID == dd.DepartmentId)?.MappingID
            }).ToList();

            var photoUrl = await _blobService.GetUrlAsync(temp.UserId, _containerName, cancellationToken) as string;

            var response = new DoctorGetResponseModel
            {
                DoctorId = temp.DoctorId,
                UserId = temp.UserId,
                LicenseNumber = temp.LicenseNumber,
                ExperienceYears = temp.ExperienceYears,
                MedicalCouncil = temp.MedicalCouncil,
                RegistrationYear = temp.RegistrationYear,
                Bio = temp.Bio,
                Languages = string.IsNullOrWhiteSpace(temp.LanguagesJson)
                    ? []
                    : (JsonSerializer.Deserialize<List<string>>(temp.LanguagesJson) ?? []),
                PublicContactEmail = temp.PublicContactEmail,
                PublicContactPhone = temp.PublicContactPhone,
                PhotoUrl = photoUrl,
                IsPubliclyListed = temp.IsPubliclyListed,
                PrimaryDepartmentID = temp.PrimaryDepartmentID,
                PrimaryDepartmentName = temp.PrimaryDepartmentName,
                PrimaryMedicalSpecialityId = temp.PrimaryMedicalSpecialityId,
                PrimaryMedicalSpecialityName = temp.PrimaryMedicalSpecialityName,
                PrimaryMedicalSpecialityPatientFacingName = temp.PrimaryMedicalSpecialityPatientFacingName,
                CreatedAt = temp.CreatedAt,
                ProfileCompletionPercentage = temp.ProfileCompletionPercentage,
                DoctorDepartments = doctorDepartments,
                DoctorSpecializations = temp.DoctorSpecializations,
                Qualifications = string.IsNullOrWhiteSpace(temp.Qualifications)
                    ? []
                    : [.. temp.Qualifications
                        .Split(',')
                        .Select(q => q.Trim())
                        .Where(q => !string.IsNullOrWhiteSpace(q))]
            };

            return response;
        }
    }
}
