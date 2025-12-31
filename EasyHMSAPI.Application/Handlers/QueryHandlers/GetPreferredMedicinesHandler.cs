using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPreferredMedicinesHandler : IRequestHandler<GetPreferredMedicinesRequestModel, GetPreferredMedicinesResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        public GetPreferredMedicinesHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<GetPreferredMedicinesResponseModel> Handle(GetPreferredMedicinesRequestModel request, CancellationToken cancellationToken)
        {
            GetPreferredMedicinesResponseModel response = new()
            {
                Success = false,
            };
            try
            {
                var existingDoctor = await _dbContext.Doctors
                 .Where(x => x.DoctorID == request.DoctorId)
                 .AsNoTracking()
                 .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                    return response;
                }

                var existingHospital = await _dbContext.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital == null)
                {
                    response.Message = "Hospital not found.";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                var list = await _dbContext.DoctorPreferredMedicines
                    .AsNoTracking()
                    .Where(d => d.DoctorId == request.DoctorId && d.HospitalId == request.HospitalId && d.IsActive)
                    .Select(x => new PreferredMedicineDataModel
                    {
                        PrefferedId = x.PreferrredId,
                        MedicineName = x.MedicineName,
                        BrandName = x.BrandName,
                        GenericName = x.GenericName,
                        Manufacturer = x.Manufacturer,
                        DosageForm = x.DosageForm,
                        Strength = x.Strength,
                        UsageDescription = x.Usage,
                        SideEffects = x.SideEffects,
                        Price = x.Price,
                        Notes = x.Notes,
                        IsActive = x.IsActive,
                        UsageCount = x.UsageCount,
                        LastModifiedAt = x.UpdatedAt,
                        LastModifiedBy = x.UpdatedBy
                    })
                    .ToListAsync(cancellationToken);
                if(list.Count > 0)
                {
                    response.Success = true;
                    response.Message = "Preferred medicines retrieved successfully.";
                    response.PreferredMedicines = list;
                }
                else
                {
                    response.Message = "No preferred medicines found.";
                }
            }
            catch(Exception ex)
            {
                response.Success = false;
                response.Message = $"An error occurred while retrieving preferred medicines: {ex.Message}";
            }

            return response;
        }
    }
}