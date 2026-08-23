using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CallNextPatientHandler : IRequestHandler<CallNextPatientRequestModel, CallQueueResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IWhatsAppQueueNotifier _notifier;

        public CallNextPatientHandler(AppDbContext context, IWhatsAppQueueNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        public async Task<CallQueueResponseModel> Handle(CallNextPatientRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty)
                return new CallQueueResponseModel { Success = false, Message = "HospitalId and DoctorId are required." };

            var todayIst = DateTime.UtcNow.AddMinutes(330).Date;

            var next = await _context.AppointmentTokens
                .Where(t => t.HospitalId == request.HospitalId && t.DoctorId == request.DoctorId && t.TokenDate == todayIst
                         && t.Status == AppConstants.QueueTokenStatus_Waiting)
                .OrderBy(t => t.QueueSequence)
                .FirstOrDefaultAsync(cancellationToken);

            if (next == null)
                return new CallQueueResponseModel { Success = false, Message = "No patients are currently waiting." };

            next.Status = AppConstants.QueueTokenStatus_Called;
            next.CalledAt = DateTime.UtcNow;

            var doctorQueue = await _context.DoctorQueues
                .FirstOrDefaultAsync(dq => dq.HospitalId == request.HospitalId && dq.DoctorId == request.DoctorId && dq.TokenDate == todayIst, cancellationToken);
            if (doctorQueue == null)
            {
                // Defensive -- AllocateTokenWithLockingAsync always creates this row on first
                // issuance for the doctor/date, so this should never actually be null here.
                doctorQueue = new DoctorQueue { HospitalId = request.HospitalId, DoctorId = request.DoctorId, TokenDate = todayIst, NextTokenNo = 1, TokenStrategy = AppConstants.TokenStrategy_Sequential };
                _context.DoctorQueues.Add(doctorQueue);
            }
            doctorQueue.CurrentServingTokenNo = next.TokenNo;

            await _context.SaveChangesAsync(cancellationToken);

            await NotifyQueueAsync(request.HospitalId, request.DoctorId, todayIst, next.TokenNo, cancellationToken);

            return new CallQueueResponseModel { Success = true, AppointmentId = next.ApptId, TokenNo = next.TokenNo };
        }

        // Broadcasts "currently serving #X" to every WAITING/CALLED patient on this doctor's queue
        // today, not just the one just called -- lets each patient gauge their own remaining wait
        // against their own already-known token number. The gateway silently no-ops for any
        // appointment it doesn't recognize (e.g. booked at the front desk), so this is safe to fire
        // for the whole queue regardless of how each appointment was originally booked.
        private async Task NotifyQueueAsync(Guid hospitalId, Guid doctorId, DateTime tokenDate, int currentToken, CancellationToken cancellationToken)
        {
            var toNotify = await _context.AppointmentTokens
                .Where(t => t.HospitalId == hospitalId && t.DoctorId == doctorId && t.TokenDate == tokenDate
                         && (t.Status == AppConstants.QueueTokenStatus_Waiting || t.Status == AppConstants.QueueTokenStatus_Called))
                .Select(t => t.ApptId)
                .ToListAsync(cancellationToken);

            await Task.WhenAll(toNotify.Select(apptId => _notifier.NotifyTokenCalledAsync(apptId, currentToken, null, cancellationToken)));
        }
    }
}
