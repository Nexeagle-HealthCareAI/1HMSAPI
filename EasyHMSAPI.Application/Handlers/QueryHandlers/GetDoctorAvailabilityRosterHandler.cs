using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Staff-facing availability roster — every doctor at one hospital with today's (or a chosen
    /// date's) TimeOff &gt; Override &gt; Template status, so reception/admin can see "who's out"
    /// at a glance instead of opening each doctor's calendar individually. Doctor listing mirrors
    /// GetHospitalDoctorsHandler (DoctorDepartments-based, not the retrofitted Doctor.HospitalId);
    /// availability resolution mirrors GetPublicDoctorsHandler's batched "IsAvailableToday" section
    /// via the shared DoctorAvailabilityResolver.
    /// </summary>
    public class GetDoctorAvailabilityRosterHandler : IRequestHandler<GetDoctorAvailabilityRosterRequestModel, GetDoctorAvailabilityRosterResponseModel>
    {
        private readonly AppDbContext _context;

        public GetDoctorAvailabilityRosterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDoctorAvailabilityRosterResponseModel> Handle(GetDoctorAvailabilityRosterRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new GetDoctorAvailabilityRosterResponseModel { Success = false, Message = "HospitalId is required." };

            // Same local/IST calendar-date convention as every other doctor-availability entry
            // point in this codebase (see DoctorAvailabilityResolver / GetPublicDoctorsHandler).
            var targetDate = (request.Date ?? DateTime.UtcNow.AddMinutes(330)).Date;

            var doctorIds = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId == request.HospitalId)
                .Select(dd => dd.DoctorID)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (doctorIds.Count == 0)
                return new GetDoctorAvailabilityRosterResponseModel { Success = true, Doctors = new() };

            var rows = await (
                from d in _context.Doctors
                where doctorIds.Contains(d.DoctorID)
                select new { d.DoctorID, d.UserID, d.PrimaryDepartmentID, d.IsOnlineNow }
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

            var pageDoctorIds = rows.Select(r => r.DoctorID).ToList();

            var timeOffRows = await _context.DoctorTimeOffs
                .Where(to => pageDoctorIds.Contains(to.DoctorID) && to.HospitalId == request.HospitalId
                          && targetDate >= to.FromDate.Date && targetDate <= to.ToDate.Date)
                .ToListAsync(cancellationToken);
            var timeOffsByDoctor = timeOffRows
                .GroupBy(to => to.DoctorID)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<DoctorTimeOff>)g.ToList());

            var overrideRows = await _context.DoctorShiftOverrides
                .Where(o => pageDoctorIds.Contains(o.DoctorID) && o.HospitalId == request.HospitalId
                         && o.StartDate <= targetDate && (!o.EndDate.HasValue || o.EndDate >= targetDate))
                .ToListAsync(cancellationToken);
            var overridesByDoctor = overrideRows
                .GroupBy(o => o.DoctorID)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<DoctorShiftOverride>)g.ToList());

            var activeTemplates = await _context.DoctorShiftTemplates.Where(t => t.IsActive).ToListAsync(cancellationToken);

            var doctors = rows.Select(r =>
            {
                var doctorTimeOffs = timeOffsByDoctor.TryGetValue(r.DoctorID, out var to) ? to : Array.Empty<DoctorTimeOff>();
                var doctorOverrides = overridesByDoctor.TryGetValue(r.DoctorID, out var ov) ? ov : Array.Empty<DoctorShiftOverride>();
                var isAvailable = DoctorAvailabilityResolver.IsAvailable(targetDate, doctorTimeOffs, doctorOverrides, activeTemplates);
                var reason = !isAvailable
                    ? doctorTimeOffs.OrderByDescending(to => to.CreatedAt).FirstOrDefault()?.Reason
                    : null;

                return new DoctorAvailabilityRosterItem
                {
                    DoctorId = r.DoctorID,
                    FullName = nameLookup.TryGetValue(r.UserID, out var n) ? n : null,
                    DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    IsAvailable = isAvailable,
                    Reason = reason,
                    IsOnlineNow = r.IsOnlineNow,
                };
            })
            .OrderBy(d => d.FullName)
            .ToList();

            return new GetDoctorAvailabilityRosterResponseModel { Success = true, Doctors = doctors };
        }
    }
}
