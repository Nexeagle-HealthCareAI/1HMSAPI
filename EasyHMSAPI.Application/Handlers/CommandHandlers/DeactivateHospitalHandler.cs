using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Deactivates a hospital/chain branch. Hospitals.IsActive already exists on the schema and is
    // read elsewhere (e.g. GetHospitalDetailsHandler) but until now nothing ever set it to false.
    public class DeactivateHospitalHandler : IRequestHandler<DeactivateHospitalRequestModel, DeactivateHospitalResponseModel>
    {
        private readonly AppDbContext _context;
        public DeactivateHospitalHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DeactivateHospitalResponseModel> Handle(DeactivateHospitalRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new DeactivateHospitalResponseModel { HospitalId = request.HospitalId };

            if (request.CallerUserId == Guid.Empty)
            {
                resp.Success = false;
                resp.Message = "Could not resolve the signed-in user.";
                return resp;
            }

            // The caller must be an admin who belongs to this hospital.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
            {
                resp.Success = false;
                resp.Message = "You don't have access to this hospital.";
                return resp;
            }
            if (!await Common.CallerGuards.IsAdminAsync(_context, request.CallerUserId, cancellationToken))
            {
                resp.Success = false;
                resp.Message = "Only an administrator can deactivate a hospital.";
                return resp;
            }

            var hospital = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
            if (hospital == null)
            {
                resp.Success = false;
                resp.Message = "Hospital not found.";
                return resp;
            }
            if (!hospital.IsActive)
            {
                resp.Success = false;
                resp.Message = "This hospital is already deactivated.";
                return resp;
            }

            hospital.IsActive = false;
            hospital.LastUpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            resp.Success = true;
            resp.Message = "Hospital deactivated.";
            return resp;
        }
    }
}
