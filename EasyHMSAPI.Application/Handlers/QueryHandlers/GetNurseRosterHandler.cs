using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Roster list for the Nursing Station admin tab. Bulk-fetch-then-dictionary, same house style
    // as GetActiveAdmissionsHandler -- one query for the roster rows, one for nurse names, one for
    // ward names, then an in-memory project.
    public class GetNurseRosterHandler : IRequestHandler<GetNurseRosterRequestModel, GetNurseRosterResponseModel>
    {
        private readonly AppDbContext _context;

        public GetNurseRosterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetNurseRosterResponseModel> Handle(GetNurseRosterRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.NurseShiftAssignment.AsNoTracking()
                .Where(a => a.HospitalId == request.HospitalId);

            if (request.ActiveOnly)
                query = query.Where(a => a.StatusCode == IpdConstants.NurseAssignmentStatus.Active);
            if (!string.IsNullOrWhiteSpace(request.WardCode))
                query = query.Where(a => a.WardCode == request.WardCode);
            if (!string.IsNullOrWhiteSpace(request.ShiftCode))
            {
                var shiftCode = request.ShiftCode.Trim().ToUpperInvariant();
                query = query.Where(a => a.ShiftCode == shiftCode);
            }
            if (request.NurseUserId.HasValue)
                query = query.Where(a => a.NurseUserId == request.NurseUserId.Value);

            var rows = await query
                .OrderBy(a => a.WardCode).ThenBy(a => a.ShiftCode).ThenByDescending(a => a.AssignedAt)
                .ToListAsync(cancellationToken);

            var resp = new GetNurseRosterResponseModel();
            if (rows.Count == 0)
                return resp;

            var nurseIds = rows.Select(r => r.NurseUserId).Distinct().ToList();
            var nurseProfiles = await _context.UserProfiles.AsNoTracking()
                .Where(up => nurseIds.Contains(up.UserID))
                .OrderByDescending(up => up.UpdatedAt)
                .ToListAsync(cancellationToken);
            var nurseNames = nurseProfiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

            var wardCodes = rows.Select(r => r.WardCode).Distinct().ToList();
            var wardRows = await _context.BedMaster.AsNoTracking()
                .Where(b => b.HospitalId == request.HospitalId && b.WardCode != null && wardCodes.Contains(b.WardCode!))
                .ToListAsync(cancellationToken);
            var wardNames = wardRows.GroupBy(b => b.WardCode!).ToDictionary(g => g.Key, g => g.First().WardName);

            resp.Items = rows.Select(r => new NurseRosterItem
            {
                NurseShiftAssignmentId = r.NurseShiftAssignmentId,
                NurseUserId = r.NurseUserId,
                NurseName = nurseNames.TryGetValue(r.NurseUserId, out var n) ? n : null,
                WardCode = r.WardCode,
                WardName = wardNames.TryGetValue(r.WardCode, out var w) ? w : null,
                ShiftCode = r.ShiftCode,
                ShiftDate = r.ShiftDate,
                StatusCode = r.StatusCode,
                AssignedAt = r.AssignedAt,
                AssignedBy = r.AssignedBy,
                UnassignedAt = r.UnassignedAt,
                UnassignedBy = r.UnassignedBy,
                Notes = r.Notes,
            }).ToList();

            return resp;
        }
    }
}
