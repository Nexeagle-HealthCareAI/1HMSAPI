using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPublicHospitalsHandler : IRequestHandler<GetPublicHospitalsRequestModel, GetPublicHospitalsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicHospitalsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicHospitalsResponseModel> Handle(GetPublicHospitalsRequestModel request, CancellationToken cancellationToken)
        {
            // Same gating GetPublicDoctorsHandler uses for its default (non-HospitalId-scanned)
            // case -- a hospital must actively opt into the platform-wide directory.
            var hospitals = await _context.Hospitals
                .Where(h => h.IsActive && !h.IsArchived && h.IsPubliclyListed)
                .Select(h => new PublicHospitalInfo
                {
                    HospitalId = h.HospitalID,
                    Name = h.Name,
                    City = h.City,
                    State = h.State,
                })
                .ToListAsync(cancellationToken);

            return new GetPublicHospitalsResponseModel { Success = true, Hospitals = hospitals };
        }
    }
}
