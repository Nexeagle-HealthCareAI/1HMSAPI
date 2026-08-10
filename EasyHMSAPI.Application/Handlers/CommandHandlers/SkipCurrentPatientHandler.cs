using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SkipCurrentPatientHandler : IRequestHandler<SkipCurrentPatientRequestModel, CallQueueResponseModel>
    {
        private readonly AppDbContext _context;

        public SkipCurrentPatientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CallQueueResponseModel> Handle(SkipCurrentPatientRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty)
                return new CallQueueResponseModel { Success = false, Message = "HospitalId and DoctorId are required." };

            var todayIst = DateTime.UtcNow.AddMinutes(330).Date;

            var doctorQueue = await _context.DoctorQueues
                .FirstOrDefaultAsync(dq => dq.HospitalId == request.HospitalId && dq.DoctorId == request.DoctorId && dq.TokenDate == todayIst, cancellationToken);
            if (doctorQueue?.CurrentServingTokenNo == null)
                return new CallQueueResponseModel { Success = false, Message = "No patient is currently being called." };

            var current = await _context.AppointmentTokens
                .FirstOrDefaultAsync(t => t.HospitalId == request.HospitalId && t.DoctorId == request.DoctorId && t.TokenDate == todayIst
                                        && t.TokenNo == doctorQueue.CurrentServingTokenNo.Value, cancellationToken);
            if (current == null || current.Status != AppConstants.QueueTokenStatus_Called)
                return new CallQueueResponseModel { Success = false, Message = "No patient is currently awaiting a skip decision." };

            if (current.SkipCount >= AppConstants.QueueMaxSkipsPerToken)
                return new CallQueueResponseModel { Success = false, Message = $"This patient has already been skipped {AppConstants.QueueMaxSkipsPerToken} times -- please handle at reception." };

            current.SkipCount++;
            current.Status = AppConstants.QueueTokenStatus_Waiting;

            // Re-queue purely by position (not slot time) -- a skip is an explicit override of the
            // normal hybrid ordering, so this reinserts the skipped token N positions later in the
            // CURRENT waiting list rather than re-deriving a slot-time position.
            var waitingList = await _context.AppointmentTokens
                .Where(t => t.HospitalId == request.HospitalId && t.DoctorId == request.DoctorId && t.TokenDate == todayIst
                         && t.Status == AppConstants.QueueTokenStatus_Waiting && t.TokenId != current.TokenId)
                .OrderBy(t => t.QueueSequence)
                .ToListAsync(cancellationToken);

            var insertIndex = Math.Min(AppConstants.QueueSkipRequeueOffset, waitingList.Count);
            waitingList.Insert(insertIndex, current);
            for (var i = 0; i < waitingList.Count; i++)
            {
                waitingList[i].QueueSequence = i + 1;
            }

            // The doctor now needs to call whoever's actually next -- clear the "who's up" pointer
            // rather than leaving it pointed at the just-skipped (now WAITING again) token. No
            // token-called push here: nobody is newly "currently serving" as a result of a skip by
            // itself (that only becomes true again once Call is invoked), and the gateway's
            // receiver only understands that one message shape -- sending it now would misleadingly
            // announce the skipped patient's own token as "currently serving".
            doctorQueue.CurrentServingTokenNo = null;

            await _context.SaveChangesAsync(cancellationToken);

            return new CallQueueResponseModel { Success = true, AppointmentId = current.ApptId, TokenNo = current.TokenNo };
        }
    }
}
