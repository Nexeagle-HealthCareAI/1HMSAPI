using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Flat, hospital-wide doctor list (no department filter) — for simple pickers like
    /// the admit form's admitting-consultant selector.</summary>
    public class GetHospitalDoctorsHandler : IRequestHandler<GetHospitalDoctorsRequestModel, GetHospitalDoctorsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalDoctorsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalDoctorsResponseModel> Handle(GetHospitalDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetHospitalDoctorsResponseModel { Success = false, Message = "HospitalId is required." };

                var rows = await (
                    from d in _context.Doctors
                    where d.HospitalId == request.HospitalId
                    select new { d.DoctorID, d.UserID, d.PrimaryDepartmentID }
                ).ToListAsync(cancellationToken);

                var userIds = rows.Select(r => r.UserID).Distinct().ToList();
                var nameByUser = await _context.UserProfiles
                    .Where(up => userIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .Select(up => new { up.UserID, up.FullName })
                    .ToListAsync(cancellationToken);
                var nameLookup = nameByUser
                    .GroupBy(n => n.UserID)
                    .ToDictionary(g => g.Key, g => g.First().FullName);

                var deptIds = rows.Where(r => r.PrimaryDepartmentID.HasValue).Select(r => r.PrimaryDepartmentID!.Value).Distinct().ToList();
                var deptNameById = await _context.Departments
                    .Where(dept => deptIds.Contains(dept.DepartmentID))
                    .ToDictionaryAsync(dept => dept.DepartmentID, dept => dept.Name, cancellationToken);

                var doctors = rows
                    .Select(r => new HospitalDoctorItem
                    {
                        DoctorId = r.DoctorID,
                        FullName = nameLookup.TryGetValue(r.UserID, out var n) ? n : null,
                        DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    })
                    .OrderBy(d => d.FullName)
                    .ToList();

                return new GetHospitalDoctorsResponseModel { Success = true, Doctors = doctors };
            }
            catch (Exception)
            {
                return new GetHospitalDoctorsResponseModel { Success = false, Message = "Error loading hospital doctors." };
            }
        }
    }
}
