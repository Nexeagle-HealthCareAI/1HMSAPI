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
            try
            {
                var existingDoctor = await _dbContext.Doctors
                     .Where(x => x.DoctorID == request.DoctorId)
                     .AsNoTracking()
                     .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Invalid doctorId";
                    return response;
                }

                var existingHospital = await _dbContext.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital == null)
                {
                    response.Message = "Invalid hospitalId";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
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
                            if (!existingPreference.IsActive)
                            {
                                response.Message = "Cannot update an inactive preferred medicine.";
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(request.Medicine.MedicineName)) existingPreference.MedicineName = request.Medicine.MedicineName.ToUpper();
                                if (!string.IsNullOrEmpty(request.Medicine.BrandName)) existingPreference.BrandName = request.Medicine.BrandName;
                                if (!string.IsNullOrEmpty(request.Medicine.GenericName)) existingPreference.GenericName = request.Medicine.GenericName;
                                if (!string.IsNullOrEmpty(request.Medicine.Manufacturer)) existingPreference.Manufacturer = request.Medicine.Manufacturer;
                                if (!string.IsNullOrEmpty(request.Medicine.DosageForm)) existingPreference.DosageForm = request.Medicine.DosageForm;
                                if (!string.IsNullOrEmpty(request.Medicine.Strength)) existingPreference.Strength = request.Medicine.Strength;
                                if (request.Medicine.Price.HasValue) existingPreference.Price = request.Medicine.Price;
                                if (!string.IsNullOrEmpty(request.Medicine.UsageDescription)) existingPreference.Usage = request.Medicine.UsageDescription;
                                if (!string.IsNullOrEmpty(request.Medicine.SideEffects)) existingPreference.SideEffects = request.Medicine.SideEffects;
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
                else if(!string.IsNullOrEmpty(request.Source))
                {
                    if(request?.Source.ToLower() == "prescription")
                    {
                        var existing = await _dbContext.DoctorPreferredMedicines
                            .Where(x =>
                                x.DoctorId == request.DoctorId &&
                                x.HospitalId == request.HospitalId &&
                                x.MedicineName != null &&
                                request.Medicine.MedicineName != null &&
                                x.MedicineName.ToLower() == request.Medicine.MedicineName.ToLower()
                            )
                            .FirstOrDefaultAsync(cancellationToken);
                        if (existing is not null)
                        {
                            existing.UsageCount = (existing.UsageCount ?? 0) + 1;
                            existing.UpdatedAt = DateTime.UtcNow;
                            existing.UpdatedBy = request.LoggedInUserId.ToString();
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            response.Success = true;
                            response.Message = "Preferred medicine usage count updated";

                        }
                        else
                        {
                            var newMed = new DoctorPreferredMedicine
                            {
                                DoctorId = request.DoctorId,
                                HospitalId = request.HospitalId,
                                MedicineName = request.Medicine?.MedicineName?.ToUpper(),
                                BrandName = request?.Medicine?.BrandName,
                                GenericName = request?.Medicine?.GenericName,
                                Manufacturer = request?.Medicine?.Manufacturer,
                                DosageForm = request?.Medicine?.DosageForm,
                                Strength = request?.Medicine?.Strength,
                                Price = request?.Medicine?.Price,
                                Usage = request?.Medicine?.UsageDescription,
                                SideEffects = request?.Medicine?.SideEffects,
                                Notes = request?.Medicine?.Notes,
                                UsageCount = 0,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = request?.LoggedInUserId.ToString(),
                                UpdatedAt = DateTime.UtcNow,
                                UpdatedBy = request?.LoggedInUserId.ToString(),
                            };

                            _dbContext.DoctorPreferredMedicines.Add(newMed);
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            response.Success = true;
                            response.Message = "Preferred medicine added";
                        }
                    }
                }
                else
                {
                    var newMed = new DoctorPreferredMedicine
                    {
                        DoctorId = request.DoctorId,
                        HospitalId = request.HospitalId,
                        MedicineName = request.Medicine.MedicineName,
                        BrandName = request.Medicine.BrandName,
                        GenericName = request.Medicine.GenericName,
                        Manufacturer = request.Medicine.Manufacturer,
                        DosageForm = request.Medicine.DosageForm,
                        Strength = request.Medicine.Strength,
                        Price = request.Medicine.Price,
                        Usage = request.Medicine.UsageDescription,
                        SideEffects = request.Medicine.SideEffects,
                        Notes = request.Medicine.Notes,
                        UsageCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = request.LoggedInUserId.ToString(),
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = request.LoggedInUserId.ToString(),
                    };

                    _dbContext.DoctorPreferredMedicines.Add(newMed);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "Preferred medicine added";
                }
            }
            catch (Exception)
            {
                response.Success = false;
                response.Message = "An error occurred while saving the preferred medicine.";
            }

            return response;
        }
    }
}