using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class MarkArrivedHandler : IRequestHandler<MarkArrivedRequestModel, IssueQueueTokenResponseModel>
    {
        private readonly AppDbContext _context;

        public MarkArrivedHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IssueQueueTokenResponseModel> Handle(MarkArrivedRequestModel request, CancellationToken cancellationToken)
        {
            if (request.AppointmentId == Guid.Empty || request.HospitalId == Guid.Empty)
                return new IssueQueueTokenResponseModel { Success = false, Message = "AppointmentId and HospitalId are required." };

            // The route's doctorId is just for REST shape parity with the spec -- the appointment's
            // own DoctorId is the source of truth, never a client-supplied value trusted blindly.
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId, cancellationToken);
            if (appointment == null)
                return new IssueQueueTokenResponseModel { Success = false, Message = "Appointment not found." };
            if (appointment.DoctorId != request.DoctorId)
                return new IssueQueueTokenResponseModel { Success = false, Message = "This appointment does not belong to the specified doctor." };
            if (appointment.HospitalId != request.HospitalId)
                return new IssueQueueTokenResponseModel { Success = false, Message = "This appointment does not belong to the specified hospital." };

            return await QueueCheckInHelper.CheckInAsync(
                _context, request.AppointmentId, AppConstants.QueueArrivalMethod_StaffOverride,
                requireGeofence: false, patientLatitude: null, patientLongitude: null, cancellationToken);
        }
    }
}
