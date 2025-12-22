using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPersonalizedDataHandler : IRequestHandler<GetPersonalizedDataRequestModel, GetPersonalizedDataResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public GetPersonalizedDataHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<GetPersonalizedDataResponseModel> Handle(GetPersonalizedDataRequestModel request, CancellationToken cancellationToken)
        {
            GetPersonalizedDataResponseModel response = new()
            {
                Success = false,
                Message = string.Empty,
                Data = null
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

                var lookupTypeUpper = request.LookupType?.ToUpper();
                var lookupType = await _dbContext.LookupTypes
                    .Where(lt => lt.LookupTypeCode.ToUpper() == lookupTypeUpper).FirstOrDefaultAsync(cancellationToken);
                if (lookupType is not null)
                {
                    var data = await _dbContext.LookupPersonals
                   .AsNoTracking()
                   .Where(lp => lp.DoctorID == request.DoctorId && lp.LookupTypeId == lookupType.LookupTypeId && lp.IsActive)
                   .Select(lp => new PersonalizedDataModel
                   {
                       PersonalId = lp.PersonalId,
                       Name = lp.Name ?? string.Empty,
                       ShortDesc = lp.ShortDesc,
                       Code = lp.Code,
                       Synonyms = lp.MetaJson,
                       UsageCount = lp.UsageCount,
                       CreatedAt = lp.CreatedAt,
                       ModifiedAt = lp.ModifiedAt
                   })
                   .ToListAsync(cancellationToken);

                    response.Success = true;
                    response.Data = data;
                    response.Message = "Personalized data retrieved successfully.";
                }
                else
                {
                    response.Message = "Lookup type not found.";
                }
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred while processing the request: " + ex.Message + ex.InnerException + ex.StackTrace;
            }
            
            return response;
        }
    }
}