using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPreferredMedicineHandler : IRequestHandler<UpsertPreferredMedicineRequestModel, UpsertPreferredMedicineResponseModel>
    {
        private readonly AppDbContext _dbContext;
        public UpsertPreferredMedicineHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UpsertPreferredMedicineResponseModel> Handle(UpsertPreferredMedicineRequestModel request, CancellationToken cancellationToken)
        {
            UpsertPreferredMedicineResponseModel response = new()
            {
                Success = false
            };

            var existingDoctor = await _dbContext.Doctors
              .Where(x => x.DoctorID == request.DoctorId)
              .FirstOrDefaultAsync(cancellationToken);
            if (existingDoctor == null)
            {
                response.Message = "Invalid doctorId";
            }

            var existingHospital = await _dbContext.Hospitals
                .Where(x => x.HospitalID == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingHospital == null)
            {
                response.Message = "Invalid hospitalId";
            }

            if(request.PreferrredId is not null)
            {
                if(request.PreferrredId <= 0)
                {
                    response.Message = "Invalid PreferredId for update.";
                }
                else
                {
                    var existingPreference = await _dbContext.DoctorPreferredMedicines
                        .FirstOrDefaultAsync(dpm => dpm.PreferrredId == request.PreferrredId && dpm.DoctorId == request.DoctorId && dpm.HospitalId == request.HospitalId, cancellationToken);
                    if (existingPreference != null)
                    {
                        existingPreference.BrandName = request.Medicine.BrandName;
                        existingPreference.GenericName = request.Medicine.GenericName;
                        existingPreference.Form = request.Medicine.Form;
                        existingPreference.StrengthValue = request.Medicine.StrengthValue;
                        existingPreference.StrengthUnit = request.Medicine.StrengthUnit;
                        existingPreference.Route = request.Medicine.Route;
                        existingPreference.Dose = request.Medicine.Dose;
                        existingPreference.Indication = request.Medicine.Indication;
                        existingPreference.Notes = request.Medicine.Notes;
                        existingPreference.UpdatedAt = DateTime.UtcNow;
                        existingPreference.HospitalId = request.HospitalId;
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        response.Success = true;
                        response.Message = "Preferred medicine updated";
                    }
                    else
                    {
                        response.Message = "Preferred medicine not found for update.";
                    }
                }  
            }
            else
            {
                var newMed = new DoctorPreferredMedicine
                {
                    BrandName = request.Medicine.BrandName,
                    GenericName = request.Medicine.GenericName,
                    Form = request.Medicine.Form,
                    StrengthValue = request.Medicine.StrengthValue,
                    StrengthUnit = request.Medicine.StrengthUnit,
                    Route = request.Medicine.Route,
                    Dose = request.Medicine.Dose,
                    Indication = request.Medicine.Indication,
                    Notes = request.Medicine.Notes,
                    MedicineId = request.Medicine.MedicineId,
                    DoctorId = request.DoctorId,
                    HospitalId = request.HospitalId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _dbContext.DoctorPreferredMedicines.Add(newMed);

                response.Success = true;
                response.Message = "Preferred medicine added";
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);

            return response;
        }
    }
}