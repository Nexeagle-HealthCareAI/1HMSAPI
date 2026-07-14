using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class SearchLookupDataHandler : IRequestHandler<SearchLookupDataRequestModel, SearchLookupDataResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public SearchLookupDataHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<SearchLookupDataResponseModel> Handle(SearchLookupDataRequestModel request, CancellationToken cancellationToken)
        {
            SearchLookupDataResponseModel response = new()
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

                if (string.IsNullOrWhiteSpace(request.LookupType))
                {
                    response.Message = "LookupType is required.";
                    return response;
                }

                var lookupType = request.LookupType.ToLower();
                var lookupTypeDetails = await _dbContext.LookupTypes
                    .Where(x => x.LookupTypeCode.ToLower() == lookupType)
                    .Select(x=> new
                    {
                        x.LookupTypeId, 
                        x.LookupTypeCode, 
                        x.Description

                    })
                    .FirstOrDefaultAsync(cancellationToken);
                if(lookupTypeDetails is not null)
                {
                    var searchTextLower = request.SearchText?.ToLower() ?? string.Empty;

                    // Matches on Name OR Code — code matters most for lookups like ICD-10, where
                    // staff often search by the exact code (e.g. "J18") rather than the condition
                    // name; Name-only matching made a code search always return zero results.
                    var personalLookupData = await _dbContext.LookupPersonals
                        .Where(x => x.HospitalID == request.HospitalId
                                    && x.DoctorID == request.DoctorId
                                    && x.LookupTypeId == lookupTypeDetails.LookupTypeId
                                    && ((x.NameLower != null && x.NameLower.Contains(searchTextLower))
                                        || (x.Code != null && x.Code.ToLower().Contains(searchTextLower))))
                        .Select(x => new PersonalLookupDataModel
                        {
                            PersonalId = x.PersonalId,
                            Code = x.Code,
                            Name = x.Name,
                            NameLower = x.NameLower,
                            ShortDesc = x.ShortDesc,
                            UsageCount = x.UsageCount
                        })
                        .ToListAsync(cancellationToken);
                    var masterLookupData = await _dbContext.LookupMasters
                        .Where(x => x.LookupTypeId == lookupTypeDetails.LookupTypeId
                                    && ((x.NameLower != null && x.NameLower.Contains(searchTextLower))
                                        || (x.Code != null && x.Code.ToLower().Contains(searchTextLower))))
                        .Select(x => new MasterLookupDataModel
                        {
                            LookupId = x.LookupId,
                            Code = x.Code,
                            Name = x.Name,
                            NameLower = x.NameLower,
                            ShortDesc = x.ShortDesc,
                            UsageCount = x.UsageCount
                        })
                        .ToListAsync(cancellationToken);

                    response.LookupType = lookupTypeDetails.LookupTypeCode.ToUpper();
                    response.LookupTypeId = lookupTypeDetails.LookupTypeId;
                    response.PersonalLookupData = personalLookupData;
                    response.MasterLookupData = masterLookupData;
                    response.Success = true;
                    response.Message = "Lookup data retrieved successfully.";
                }
                else
                {
                    response.Message = "LookupType not found.";
                    return response;
                }
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
