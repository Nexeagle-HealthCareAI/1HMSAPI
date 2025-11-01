using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalDetailsHandler : IRequestHandler<GetHospitalDetailsRequestModel, GetHospitalDetailsResponseModel?>
    {
        private readonly AppDbContext _context;

        public GetHospitalDetailsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalDetailsResponseModel?> Handle(GetHospitalDetailsRequestModel request, CancellationToken cancellationToken)
        {
            var hospital = await _context.Hospitals
                .FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);

            if (hospital == null)
            {
                return null;
            }

            var profileStatus = await _context.HospitalProfileStatuses.FirstOrDefaultAsync(hps => hps.HospitalID == hospital.HospitalID, cancellationToken);
            var hospitalDepartmentMappingId = await _context.HospitalDepartmentMappings
                .Where(hdm => hdm.HospitalID == hospital.HospitalID)
                .Select(hdm => (Guid?)hdm.MappingID)
                .FirstOrDefaultAsync(cancellationToken);

            return new GetHospitalDetailsResponseModel
            {
                HospitalId = hospital.HospitalID,
                HospitalDepartmentMappingId = hospitalDepartmentMappingId,
                Name = hospital.Name,
                Type = hospital.Type,
                Email = hospital.Email,
                Contact = hospital.Contact,
                AlternateContact = hospital.AlternateContact,
                Website = hospital.Website,
                Location = hospital.Location,
                City = hospital.City,
                State = hospital.State,
                Country = hospital.Country,
                Pincode = hospital.Pincode,
                RegistrationNumber = hospital.RegistrationNumber,
                TimeZone = hospital.TimeZone,
                
                IsActive = hospital.IsActive,
                CreatedAt = hospital.CreatedAt,
                LastUpdatedAt = hospital.LastUpdatedAt,
                ProfileStatus = profileStatus == null ? null : new HospitalProfileStatusDto
                {
                    IsBasicInfoComplete = profileStatus.IsBasicInfoComplete,
                    IsContactInfoComplete = profileStatus.IsContactInfoComplete,
                    IsLocationInfoComplete = profileStatus.IsLocationInfoComplete,
                    ProfileCompletionPercent = profileStatus.ProfileCompletionPercent,
                    LastUpdatedAt = profileStatus.LastUpdatedAt
                }
            };
        }
    }
}