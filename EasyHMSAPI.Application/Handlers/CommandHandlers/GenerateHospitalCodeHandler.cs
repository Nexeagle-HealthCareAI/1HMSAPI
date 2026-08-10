using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GenerateHospitalCodeHandler : IRequestHandler<GenerateHospitalCodeRequestModel, GenerateHospitalCodeResponseModel>
    {
        private readonly AppDbContext _context;

        public GenerateHospitalCodeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GenerateHospitalCodeResponseModel> Handle(GenerateHospitalCodeRequestModel request, CancellationToken cancellationToken)
        {
            var hospital = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
            if (hospital == null)
                return new GenerateHospitalCodeResponseModel { Success = false, Message = "Hospital not found." };

            if (!string.IsNullOrEmpty(hospital.HospitalCode))
                return new GenerateHospitalCodeResponseModel { Success = true, HospitalCode = hospital.HospitalCode, Message = "Hospital already has a code." };

            hospital.HospitalCode = await HospitalCodeHelper.GenerateUniqueCodeAsync(_context, cancellationToken);
            hospital.LastUpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return new GenerateHospitalCodeResponseModel { Success = true, HospitalCode = hospital.HospitalCode, Message = "Hospital code generated." };
        }
    }
}
