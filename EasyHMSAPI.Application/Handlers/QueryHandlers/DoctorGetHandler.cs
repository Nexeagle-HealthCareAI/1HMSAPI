using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Data.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorGetHandler : IRequestHandler<DoctorGetRequestModel, DoctorGetResponseModel?>
    {
        private readonly AppDbContext _context;

        public DoctorGetHandler(AppDbContext context)
        {
            _context = context;
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
                                  d.PrimaryDepartmentID,
                                  PrimaryDepartmentName = d.PrimaryDepartment != null ? d.PrimaryDepartment.Name : null,
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

            var response = new DoctorGetResponseModel
            {
                DoctorId = temp.DoctorId,
                UserId = temp.UserId,
                LicenseNumber = temp.LicenseNumber,
                ExperienceYears = temp.ExperienceYears,
                MedicalCouncil = temp.MedicalCouncil,
                RegistrationYear = temp.RegistrationYear,
                Bio = temp.Bio,
                PrimaryDepartmentID = temp.PrimaryDepartmentID,
                PrimaryDepartmentName = temp.PrimaryDepartmentName,
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
