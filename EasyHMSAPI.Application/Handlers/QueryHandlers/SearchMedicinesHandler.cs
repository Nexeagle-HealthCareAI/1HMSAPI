using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class SearchMedicinesHandler : IRequestHandler<SearchMedicinesRequestModel, SearchMedicinesResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public SearchMedicinesHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<SearchMedicinesResponseModel> Handle(SearchMedicinesRequestModel request, CancellationToken cancellationToken)
        {
            SearchMedicinesResponseModel response = new()
            {
                HospitalId = request.HospitalId,
                DoctorId = request.DoctorId,
                Success = false,
            };
            try
            {
                var existingDoctor = await _dbContext.Doctors
                    .Where(x => x.DoctorID == request.DoctorId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                    return response;
                }

                var existingHospital = await _dbContext.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
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

                var searchTextLower = request.SearchText?.ToLower() ?? string.Empty;

                var personalMedicines = await _dbContext.DoctorPreferredMedicines
                    .Where(x => x.HospitalId == request.HospitalId
                                && x.DoctorId == request.DoctorId
                                && (x.MedicineName != null && x.MedicineName.Contains(searchTextLower)))
                    .Select(x => new PersonalMedicineDataModel
                    {
                        MedicineName = x.MedicineName,
                        GenericName = x.GenericName,
                        BrandName = x.BrandName,
                        Manufacturer = x.Manufacturer,
                        DosageForm = x.DosageForm,
                        Strength = x.Strength,
                        UsageDescription = x.Usage,
                        SideEffects = x.SideEffects,
                        Price = x.Price
                    })
                    .ToListAsync(cancellationToken);

                var masterMedicines = await _dbContext.MedicineMaster
                    .Where(x => x.MedicineName != null && x.MedicineName.Contains(searchTextLower))
                    .Select(x => new MasterMedicineDataModel
                    {
                        MedicineName = x.MedicineName,
                        GenericName = x.GenericName,
                        BrandName = x.BrandName,
                        Manufacturer = x.Manufacturer,
                        DosageForm = x.DosageForm,
                        Strength = x.Strength,
                        UsageDescription = x.UsageDescription,
                        SideEffects = x.SideEffects,
                        Price = x.PriceApprox
                    })
                    .ToListAsync(cancellationToken);

                response.PersonalMedicine = personalMedicines;
                response.MasterMedicine = masterMedicines;
                response.Success = true;
                response.Message = "Medicines retrieved successfully.";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
