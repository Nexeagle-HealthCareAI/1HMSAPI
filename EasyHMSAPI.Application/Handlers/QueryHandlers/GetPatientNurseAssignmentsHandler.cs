using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Assignment list for one patient -- bulk-fetch-then-dictionary, same house style as
    // GetNurseRosterHandler.
    public class GetPatientNurseAssignmentsHandler : IRequestHandler<GetPatientNurseAssignmentsRequestModel, GetPatientNurseAssignmentsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientNurseAssignmentsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientNurseAssignmentsResponseModel> Handle(GetPatientNurseAssignmentsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.PatientNurseAssignment.AsNoTracking()
                .Where(a => a.HospitalId == request.HospitalId && a.AdmissionId == request.AdmissionId);

            if (request.ActiveOnly)
                query = query.Where(a => a.StatusCode == IpdConstants.NurseAssignmentStatus.Active);

            var rows = await query
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync(cancellationToken);

            var resp = new GetPatientNurseAssignmentsResponseModel();
            if (rows.Count == 0)
                return resp;

            var nurseIds = rows.Select(r => r.NurseUserId).Distinct().ToList();
            var nurseProfiles = await _context.UserProfiles.AsNoTracking()
                .Where(up => nurseIds.Contains(up.UserID))
                .OrderByDescending(up => up.UpdatedAt)
                .ToListAsync(cancellationToken);
            var nurseNames = nurseProfiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

            resp.Items = rows.Select(r => new PatientNurseAssignmentItem
            {
                PatientNurseAssignmentId = r.PatientNurseAssignmentId,
                NurseUserId = r.NurseUserId,
                NurseName = nurseNames.TryGetValue(r.NurseUserId, out var n) ? n : null,
                AdmissionId = r.AdmissionId,
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
