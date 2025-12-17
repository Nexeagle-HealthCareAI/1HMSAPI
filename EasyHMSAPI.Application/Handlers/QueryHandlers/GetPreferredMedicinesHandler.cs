using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPreferredMedicinesHandler : IRequestHandler<GetPreferredMedicinesRequestModel, ResponseModels.QueryResponseModels.GetPreferredMedicinesResponseModel>
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
                  .FirstOrDefaultAsync(cancellationToken);
                if(existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                }

                var existingHospital = await _dbContext.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Hospital not found.");
                if (existingHospital == null) 
                { 
                    response.Message = "Hospital not found.";
                }
                else
                {
                    if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                    {
                        response.Message = "Doctor is not associated with the specified hospital.";
                    }
                }

                var list = await _dbContext.DoctorPreferredMedicines
                    .AsNoTracking()
                    .Where(d => d.DoctorId == request.DoctorId && d.HospitalId == request.HospitalId && d.IsActive)
                    .Select(d => new PreferredMedicineDataModel
                    {
                        PrefferedId = d.PreferrredId,
                        GenericName = d.GenericName,
                        BrandName = d.BrandName ?? string.Empty,
                        Form = d.Form ?? string.Empty,
                        StrengthValue = d.StrengthValue ?? string.Empty,
                        StrengthUnit = d.StrengthUnit ?? string.Empty,
                        Route = d.Route ?? string.Empty,
                        Dose = d.Dose ?? string.Empty,
                        Indication = d.Indication ?? string.Empty,
                        Notes = d.Notes ?? string.Empty,
                        MedicineId = d.MedicineId ?? string.Empty,
                        UsageCount = d.UsageCount,
                        LastModifiedAt = d.UpdatedAt
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