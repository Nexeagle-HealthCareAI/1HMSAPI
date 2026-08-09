using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateDoctorOnlineStatusHandler : IRequestHandler<UpdateDoctorOnlineStatusRequestModel, UpdateDoctorOnlineStatusResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateDoctorOnlineStatusHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateDoctorOnlineStatusResponseModel> Handle(UpdateDoctorOnlineStatusRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new UpdateDoctorOnlineStatusResponseModel { Success = false, Message = "DoctorId is required." };

            // Confirm the doctor genuinely belongs to this hospital before letting this hospital's
            // staff flip their online-status flag — DoctorDepartments is the source of truth, not
            // the single retrofitted Doctor.HospitalId field (see UpdateDoctorPublicListingHandler).
            var belongsToHospital = await _context.DoctorDepartments
                .AnyAsync(dd => dd.DoctorID == request.DoctorId && dd.HospitalId == request.HospitalId, cancellationToken);
            if (!belongsToHospital)
                return new UpdateDoctorOnlineStatusResponseModel { Success = false, Message = "Doctor not found at this hospital." };

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
            if (doctor == null)
                return new UpdateDoctorOnlineStatusResponseModel { Success = false, Message = "Doctor not found." };

            doctor.IsOnlineNow = request.IsOnlineNow;
            await _context.SaveChangesAsync(cancellationToken);

            return new UpdateDoctorOnlineStatusResponseModel { Success = true, Message = "Doctor online status saved." };
        }
    }
}
