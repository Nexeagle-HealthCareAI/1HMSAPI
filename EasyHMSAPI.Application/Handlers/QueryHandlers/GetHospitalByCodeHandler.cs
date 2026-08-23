using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalByCodeHandler : IRequestHandler<GetHospitalByCodeRequestModel, GetHospitalByCodeResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalByCodeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalByCodeResponseModel> Handle(GetHospitalByCodeRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.HospitalCode))
                return new GetHospitalByCodeResponseModel { Success = false, Message = "Hospital code is required." };

            var code = request.HospitalCode.Trim().ToUpperInvariant();
            var hospital = await _context.Hospitals
                .AsNoTracking()
                .Where(h => h.HospitalCode == code && h.IsActive && !h.IsArchived)
                .Select(h => new { h.HospitalID, h.Name, h.City })
                .FirstOrDefaultAsync(cancellationToken);

            if (hospital == null)
                return new GetHospitalByCodeResponseModel { Success = false, Message = "Hospital code not recognized." };

            return new GetHospitalByCodeResponseModel
            {
                Success = true,
                HospitalId = hospital.HospitalID,
                Name = hospital.Name,
                City = hospital.City,
            };
        }
    }
}
