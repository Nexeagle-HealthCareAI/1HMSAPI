using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Full "Referred by" assignment history for one admission, newest first — each row is
    /// one referrer's tenure span (AssignedAt -> UnassignedAt, or "current" while ACTIVE). Unlike
    /// GetAdmissionDoctorHistoryHandler, no join is needed to resolve a display name — ReferrerName/
    /// ReferrerType are snapshotted onto the row at write time by AdmissionReferrerAssignmentHelper,
    /// so history reads correctly even if the underlying Referrer master row is later renamed.</summary>
    public class GetAdmissionReferrerHistoryHandler : IRequestHandler<GetAdmissionReferrerHistoryRequestModel, GetAdmissionReferrerHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionReferrerHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionReferrerHistoryResponseModel> Handle(GetAdmissionReferrerHistoryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetAdmissionReferrerHistoryResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var items = await _context.AdmissionReferrerAssignment
                    .Where(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId)
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new AdmissionReferrerHistoryItem
                    {
                        AssignmentId = a.AssignmentId,
                        ReferralSource = a.ReferralSource,
                        ReferrerId = a.ReferrerId,
                        ReferrerName = a.ReferrerName,
                        ReferrerType = a.ReferrerType,
                        AssignedAt = a.AssignedAt,
                        AssignedBy = a.AssignedBy,
                        UnassignedAt = a.UnassignedAt,
                        UnassignedBy = a.UnassignedBy,
                        StatusCode = a.StatusCode,
                    })
                    .ToListAsync(cancellationToken);

                return new GetAdmissionReferrerHistoryResponseModel { Success = true, Items = items };
            }
            catch (Exception)
            {
                return new GetAdmissionReferrerHistoryResponseModel { Success = false, Message = "Error loading referrer history." };
            }
        }
    }
}
