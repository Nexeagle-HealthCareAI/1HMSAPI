using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Bulk counterpart to GetPatientNurseAssignmentsHandler -- same bulk-fetch-then-dictionary
    // house style, just scoped to many admissions instead of one, for the ward board's per-bed
    // "who's assigned" column (previously one request per occupied bed).
    public class GetPatientNurseAssignmentsBulkHandler : IRequestHandler<GetPatientNurseAssignmentsBulkRequestModel, GetPatientNurseAssignmentsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientNurseAssignmentsBulkHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientNurseAssignmentsResponseModel> Handle(GetPatientNurseAssignmentsBulkRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new GetPatientNurseAssignmentsResponseModel();
            if (request.AdmissionIds.Count == 0)
                return resp;

            var query = _context.PatientNurseAssignment.AsNoTracking()
                .Where(a => a.HospitalId == request.HospitalId && request.AdmissionIds.Contains(a.AdmissionId));

            if (request.ActiveOnly)
                query = query.Where(a => a.StatusCode == IpdConstants.NurseAssignmentStatus.Active);

            var rows = await query
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync(cancellationToken);

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
