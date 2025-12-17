using EasyHMSAPI.Application.Helpers.Interfaces;
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
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        public UpsertPreferredMedicineHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
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
            else
            {
                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                }
            }

            if (request.PreferrredId is not null)
            {
                if (request.PreferrredId <= 0)
                {
                    response.Message = "Invalid PreferredId for update.";
                }
                else
                {
                    var existingPreference = await _dbContext.DoctorPreferredMedicines
                        .FirstOrDefaultAsync(dpm => dpm.PreferrredId == request.PreferrredId && dpm.DoctorId == request.DoctorId && dpm.HospitalId == request.HospitalId, cancellationToken);
                    if (existingPreference != null)
                    {
                        if(!existingPreference.IsActive)
                        {
                            response.Message = "Cannot update an inactive preferred medicine.";
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(request.Medicine.BrandName)) existingPreference.BrandName = request.Medicine.BrandName;
                            if (!string.IsNullOrEmpty(request.Medicine.GenericName)) existingPreference.GenericName = request.Medicine.GenericName;
                            if (!string.IsNullOrEmpty(request.Medicine.Form)) existingPreference.Form = request.Medicine.Form;
                            if (!string.IsNullOrEmpty(request.Medicine.StrengthValue)) existingPreference.StrengthValue = request.Medicine.StrengthValue;
                            if (!string.IsNullOrEmpty(request.Medicine.StrengthUnit)) existingPreference.StrengthUnit = request.Medicine.StrengthUnit;
                            if (!string.IsNullOrEmpty(request.Medicine.Route)) existingPreference.Route = request.Medicine.Route;
                            if (!string.IsNullOrEmpty(request.Medicine.Dose)) existingPreference.Dose = request.Medicine.Dose;
                            if (!string.IsNullOrEmpty(request.Medicine.Indication)) existingPreference.Indication = request.Medicine.Indication;
                            if (!string.IsNullOrEmpty(request.Medicine.Notes)) existingPreference.Notes = request.Medicine.Notes;
                            existingPreference.UpdatedAt = DateTime.UtcNow;
                            existingPreference.UpdatedBy = request.LoggedInUserId.ToString();
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            response.Success = true;
                            response.Message = "Preferred medicine updated";
                        } 
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
                    UsageCount = 0,
                    IsActive = true,
                    CreatedBy = request.LoggedInUserId.ToString(),
                };

                _dbContext.DoctorPreferredMedicines.Add(newMed);
                await _dbContext.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Preferred medicine added";
            }

            return response;
        }
    }
}