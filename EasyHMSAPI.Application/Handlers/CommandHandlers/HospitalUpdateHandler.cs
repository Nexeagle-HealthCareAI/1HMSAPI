using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class HospitalUpdateHandler : IRequestHandler<HospitalUpdateRequestModel, HospitalUpdateResponseModel>
    {
        private readonly AppDbContext _context;
        public HospitalUpdateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HospitalUpdateResponseModel> Handle(HospitalUpdateRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
            {
                return new HospitalUpdateResponseModel
                {
                    Success = false,
                    Message = "HospitalId is required.",
                    HospitalId = null
                };
            }

            var hospital = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
            if (hospital == null)
            {
                return new HospitalUpdateResponseModel
                {
                    Success = false,
                    Message = "Hospital not found.",
                    HospitalId = null
                };
            }
            else
            {
                hospital.Name = !string.IsNullOrEmpty(request.Name) ? request.Name : hospital.Name;
                hospital.Type = !string.IsNullOrEmpty(request.Type) ? request.Type : hospital.Type;
                hospital.Email = !string.IsNullOrEmpty(request.Email) ? request.Email : hospital.Email;
                hospital.Contact = !string.IsNullOrEmpty(request.Contact) ? request.Contact : hospital.Contact;
                hospital.Location = !string.IsNullOrEmpty(request.Location) ? request.Location : hospital.Location;
                hospital.RegistrationNumber = !string.IsNullOrEmpty(request.RegistrationNumber) ? request.RegistrationNumber : hospital.RegistrationNumber;
                hospital.AlternateContact = !string.IsNullOrEmpty(request.AlternateContact) ? request.AlternateContact : hospital.AlternateContact;
                hospital.Website = !string.IsNullOrEmpty(request.Website) ? request.Website : hospital.Website;
                hospital.City = !string.IsNullOrEmpty(request.City) ? request.City : hospital.City;
                hospital.State = !string.IsNullOrEmpty(request.State) ? request.State : hospital.State;
                hospital.Country = !string.IsNullOrEmpty(request.Country) ? request.Country : hospital.Country;
                hospital.Pincode = !string.IsNullOrEmpty(request.Pincode) ? request.Pincode : hospital.Pincode;
                hospital.TimeZone = !string.IsNullOrEmpty(request.TimeZone) ? request.TimeZone : hospital.TimeZone;

                var hospitalProfileStatus = await _context.HospitalProfileStatuses.FirstOrDefaultAsync(hps => hps.HospitalID == hospital.HospitalID, cancellationToken);
                if (hospitalProfileStatus != null)
                {
                    int isBasicInfoComplete = (!string.IsNullOrEmpty(hospital.Name) && !string.IsNullOrEmpty(hospital.Type)) ? 1 : 0;
                    int isContactInfoComplete = (!string.IsNullOrEmpty(hospital.Contact) && !string.IsNullOrEmpty(hospital.Email)) ? 1 : 0;
                    int isLocationInfoComplete = (!string.IsNullOrEmpty(hospital.Location) && !string.IsNullOrEmpty(hospital.City) && !string.IsNullOrEmpty(hospital.State) && !string.IsNullOrEmpty(hospital.Country) && !string.IsNullOrEmpty(hospital.Pincode)) ? 1 : 0;
                    int totalCompletedSections = isBasicInfoComplete + isContactInfoComplete + isLocationInfoComplete;
                    int profileCompletionPercent = (int)((totalCompletedSections / 3.0) * 100);

                    hospitalProfileStatus.IsBasicInfoComplete = isBasicInfoComplete == 1;
                    hospitalProfileStatus.IsContactInfoComplete = isContactInfoComplete == 1;
                    hospitalProfileStatus.IsLocationInfoComplete = isLocationInfoComplete == 1;
                    hospitalProfileStatus.ProfileCompletionPercent = profileCompletionPercent;
                    hospitalProfileStatus.LastUpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new HospitalUpdateResponseModel
                {
                    Success = true,
                    Message = "Hospital details successfully updated.",
                    HospitalId = request.HospitalId
                };
            }
        }
    }
} 