using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetQueueTokenStatusHandler : IRequestHandler<GetQueueTokenStatusRequestModel, GetQueueTokenStatusResponseModel>
    {
        private readonly AppDbContext _context;

        public GetQueueTokenStatusHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetQueueTokenStatusResponseModel> Handle(GetQueueTokenStatusRequestModel request, CancellationToken cancellationToken)
        {
            var token = await _context.AppointmentTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ApptId == request.AppointmentId, cancellationToken);
            if (token == null)
                return new GetQueueTokenStatusResponseModel { Success = false, Message = "You haven't checked in yet." };

            var doctorQueue = await _context.DoctorQueues
                .AsNoTracking()
                .FirstOrDefaultAsync(dq => dq.HospitalId == token.HospitalId && dq.DoctorId == token.DoctorId && dq.TokenDate == token.TokenDate, cancellationToken);

            int? positionInQueue = null;
            int? estimatedWaitMinutes = null;
            if (token.Status == AppConstants.QueueTokenStatus_Waiting || token.Status == AppConstants.QueueTokenStatus_Called)
            {
                positionInQueue = await _context.AppointmentTokens
                    .Where(t => t.HospitalId == token.HospitalId && t.DoctorId == token.DoctorId && t.TokenDate == token.TokenDate
                             && (t.Status == AppConstants.QueueTokenStatus_Waiting || t.Status == AppConstants.QueueTokenStatus_Called)
                             && t.QueueSequence <= token.QueueSequence)
                    .CountAsync(cancellationToken);
                estimatedWaitMinutes = Math.Max(0, positionInQueue.Value - 1) * AppConstants.QueueAverageConsultMinutes;
            }

            return new GetQueueTokenStatusResponseModel
            {
                Success = true,
                TokenNo = token.TokenNo,
                Status = token.Status,
                CurrentServingTokenNo = doctorQueue?.CurrentServingTokenNo,
                PositionInQueue = positionInQueue,
                EstimatedWaitMinutes = estimatedWaitMinutes,
            };
        }
    }
}
