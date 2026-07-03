using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetRestraintOrdersHandler : IRequestHandler<GetRestraintOrdersRequestModel, GetRestraintOrdersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetRestraintOrdersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRestraintOrdersResponseModel> Handle(GetRestraintOrdersRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetRestraintOrdersResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var orders = await _context.RestraintOrder
                    .Where(r => r.HospitalId == request.HospitalId && r.AdmissionId == request.AdmissionId)
                    .OrderBy(r => r.StatusCode == IpdConstants.RestraintStatus.Active ? 0 : 1)
                    .ThenByDescending(r => r.StartedAt)
                    .Select(r => new RestraintOrderItem
                    {
                        RestraintOrderId = r.RestraintOrderId,
                        RestraintType = r.RestraintType,
                        Reason = r.Reason,
                        OrderedByDoctorName = r.OrderedByDoctorName,
                        OrderedAt = r.OrderedAt,
                        StartedAt = r.StartedAt,
                        StartedBy = r.StartedBy,
                        MonitoringIntervalMins = r.MonitoringIntervalMins,
                        FamilyNotified = r.FamilyNotified,
                        FamilyNotificationNotes = r.FamilyNotificationNotes,
                        ReleasedAt = r.ReleasedAt,
                        ReleasedBy = r.ReleasedBy,
                        ReleaseReason = r.ReleaseReason,
                        StatusCode = r.StatusCode,
                        Notes = r.Notes,
                    })
                    .ToListAsync(cancellationToken);

                return new GetRestraintOrdersResponseModel { Success = true, Orders = orders };
            }
            catch (Exception)
            {
                return new GetRestraintOrdersResponseModel { Success = false, Message = "Error loading restraint orders." };
            }
        }
    }
}
