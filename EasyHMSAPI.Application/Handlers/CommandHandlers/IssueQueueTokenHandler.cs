using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Patient self-check-in via the OPD QR flow. See QueueCheckInHelper for the shared idempotency/
    // token-allocation logic also used by the staff-facing mark-arrived override.
    public class IssueQueueTokenHandler : IRequestHandler<IssueQueueTokenRequestModel, IssueQueueTokenResponseModel>
    {
        private readonly AppDbContext _context;

        public IssueQueueTokenHandler(AppDbContext context)
        {
            _context = context;
        }

        public Task<IssueQueueTokenResponseModel> Handle(IssueQueueTokenRequestModel request, CancellationToken cancellationToken)
        {
            if (request.AppointmentId == Guid.Empty)
                return Task.FromResult(new IssueQueueTokenResponseModel { Success = false, Message = "AppointmentId is required." });

            return QueueCheckInHelper.CheckInAsync(
                _context, request.AppointmentId, AppConstants.QueueArrivalMethod_Geofence,
                requireGeofence: true, request.Latitude, request.Longitude, cancellationToken);
        }
    }
}
