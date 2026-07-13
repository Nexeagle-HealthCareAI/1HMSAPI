using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateDoctorPublicListingHandler : IRequestHandler<UpdateDoctorPublicListingRequestModel, UpdateDoctorPublicListingResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateDoctorPublicListingHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateDoctorPublicListingResponseModel> Handle(UpdateDoctorPublicListingRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new UpdateDoctorPublicListingResponseModel { Success = false, Message = "DoctorId is required." };

            // Confirm the doctor genuinely belongs to this hospital before letting this hospital's
            // admin flip their public-listing flag — DoctorDepartments is the source of truth, not
            // the single retrofitted Doctor.HospitalId field (see GetDoctorFeesHandler).
            var belongsToHospital = await _context.DoctorDepartments
                .AnyAsync(dd => dd.DoctorID == request.DoctorId && dd.HospitalId == request.HospitalId, cancellationToken);
            if (!belongsToHospital)
                return new UpdateDoctorPublicListingResponseModel { Success = false, Message = "Doctor not found at this hospital." };

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
            if (doctor == null)
                return new UpdateDoctorPublicListingResponseModel { Success = false, Message = "Doctor not found." };

            doctor.IsPubliclyListed = request.IsPubliclyListed;
            await _context.SaveChangesAsync(cancellationToken);

            return new UpdateDoctorPublicListingResponseModel { Success = true, Message = "Doctor public-listing preference saved." };
        }
    }
}
