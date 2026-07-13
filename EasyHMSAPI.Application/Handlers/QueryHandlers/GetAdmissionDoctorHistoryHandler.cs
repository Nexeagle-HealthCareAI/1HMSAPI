using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Full doctor-assignment history for one admission, newest first — each row is one
    /// doctor's tenure span (AssignedAt -> UnassignedAt, or "current" while ACTIVE). Resolves doctor
    /// names via the same Doctors -> UserProfiles join GetActiveAdmissionsHandler already uses for
    /// PrimaryDoctorName.</summary>
    public class GetAdmissionDoctorHistoryHandler : IRequestHandler<GetAdmissionDoctorHistoryRequestModel, GetAdmissionDoctorHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionDoctorHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionDoctorHistoryResponseModel> Handle(GetAdmissionDoctorHistoryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetAdmissionDoctorHistoryResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var rows = await _context.AdmissionDoctorAssignment
                    .Where(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId)
                    .OrderByDescending(a => a.AssignedAt)
                    .ToListAsync(cancellationToken);

                var doctorIds = rows.Select(r => r.DoctorId).Distinct().ToList();
                var doctorUserIds = await _context.Doctors
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Select(d => new { d.DoctorID, d.UserID })
                    .ToListAsync(cancellationToken);
                var userIds = doctorUserIds.Select(d => d.UserID).Distinct().ToList();
                var nameByUser = await _context.UserProfiles
                    .Where(up => userIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .Select(up => new { up.UserID, up.FullName })
                    .ToListAsync(cancellationToken);
                var nameByUserLookup = nameByUser.GroupBy(n => n.UserID).ToDictionary(g => g.Key, g => g.First().FullName);
                var doctorNameById = doctorUserIds.ToDictionary(d => d.DoctorID, d => nameByUserLookup.TryGetValue(d.UserID, out var n) ? n : null);

                var items = rows.Select(r => new AdmissionDoctorHistoryItem
                {
                    AssignmentId = r.AssignmentId,
                    DoctorId = r.DoctorId,
                    DoctorName = doctorNameById.TryGetValue(r.DoctorId, out var dn) ? dn : null,
                    AssignedAt = r.AssignedAt,
                    AssignedBy = r.AssignedBy,
                    UnassignedAt = r.UnassignedAt,
                    UnassignedBy = r.UnassignedBy,
                    StatusCode = r.StatusCode,
                }).ToList();

                return new GetAdmissionDoctorHistoryResponseModel { Success = true, Items = items };
            }
            catch (Exception)
            {
                return new GetAdmissionDoctorHistoryResponseModel { Success = false, Message = "Error loading doctor history." };
            }
        }
    }
}
