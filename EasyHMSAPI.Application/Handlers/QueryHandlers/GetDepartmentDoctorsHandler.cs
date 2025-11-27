using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDepartmentDoctorsHandler : IRequestHandler<GetDepartmentDoctorsRequestModel, GetDepartmentDoctorsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetDepartmentDoctorsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDepartmentDoctorsResponseModel> Handle(GetDepartmentDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            var doctorsQuery = _context.DoctorDepartments
                .Where(dd => dd.DepartmentID == request.DepartmentId && dd.HospitalId == request.HospitalId)
                .Join(_context.Doctors,
                    dd => dd.DoctorID,
                    d => d.DoctorID,
                    (dd, d) => d)
                .Join(_context.Users.Where(u => u.UserStatusId != (int)UserStatusEnum.Revoked),
                    d => d.UserID,
                    u => u.UserID,
                    (d, u) => new { d.DoctorID, d.UserID, d.LicenseNumber, d.Qualification })
                .Join(_context.UserProfiles,
                    du => du.UserID,
                    up => up.UserID,
                    (du, up) => new
                    {
                        du.DoctorID,
                        du.LicenseNumber,
                        du.Qualification,
                        DoctorName = up.FullName ?? string.Empty
                    });

            var doctorsList = await doctorsQuery.ToListAsync(cancellationToken);

            var doctors = doctorsList.Select(d => new DepartmentDoctorInfo
            {
                DoctorId = d.DoctorID,
                DoctorName = d.DoctorName,
                LicenseNumber = d.LicenseNumber ?? string.Empty,
                Qualifications = !string.IsNullOrEmpty(d.Qualification)
                    ? d.Qualification
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(q => q.Trim())
                        .Where(q => !string.IsNullOrWhiteSpace(q))
                        .ToList()
                    : new List<string>(),

                Specializations = _context.DoctorSpecializations
                    .Where(ds => ds.DoctorID == d.DoctorID &&
                                 ds.Specialization != null &&
                                 ds.Specialization.IsActive)
                    .Select(ds => ds.Specialization.Name)
                    .AsEnumerable()
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                                   name.Trim().Length > 2 &&
                                   name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c)))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

            return new GetDepartmentDoctorsResponseModel { Doctors = doctors };
        }
    }
}
