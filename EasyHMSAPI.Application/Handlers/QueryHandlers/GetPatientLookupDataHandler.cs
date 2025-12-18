using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientLookupDataHandler : IRequestHandler<GetPatientLookupDataRequestModel, GetPatientLookupDataResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public GetPatientLookupDataHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<GetPatientLookupDataResponseModel> Handle(GetPatientLookupDataRequestModel request, CancellationToken cancellationToken)
        {
            GetPatientLookupDataResponseModel response = new()
            {
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

                var existingPersonalLookup = await _dbContext.LookupPersonals
                    .Where(x => x.HospitalID == request.HospitalId && x.DoctorID == request.DoctorId)
                    .ToListAsync(cancellationToken);
                
                if (existingPersonalLookup == null || !existingPersonalLookup.Any())
                {
                    response.Message = "No personal lookup data found for the specified hospital and doctor.";
                    return response;
                }

                var lookupTypes = await _dbContext.LookupTypes.ToListAsync(cancellationToken);

                var groupedData = existingPersonalLookup
                    .GroupBy(lp => lp.LookupTypeId)
                    .Select(g => new LookIpDetailsDataModel
                    {
                        LookupTypeId = g.Key,
                        Count = g.Count(),
                        PersonalData = g
                            .OrderByDescending(x => x.UsageCount)
                            .ThenBy(x => x.Name)
                            .Take(20)
                            .Select(x => new LookupPersonalDataModel
                            {
                                PersonalId = x.PersonalId,
                                Code = x.Code,
                                Name = x.Name,
                                NameLower = x.Name?.ToLower(),
                                ShortDesc = x.ShortDesc,
                                UsageCount = x.UsageCount
                            }).ToList()
                    })
                    .ToList();

                var items = groupedData.Select(g => {
                    var type = lookupTypes.FirstOrDefault(t => t.LookupTypeId == g.LookupTypeId);
                    return new LookIpDetailsDataModel
                    {
                        LookupTypeId = g.LookupTypeId,
                        LookupType = type?.LookupTypeCode ?? "",
                        Count = g.Count,
                        PersonalData = g.PersonalData,
                        GeneratedAtUtc = DateTime.UtcNow
                    };
                }).ToList();

                response.HospitalId = request.HospitalId;
                response.DoctorId = request.DoctorId;
                response.LookupType = "All";
                response.TotalTypes = items.Count;
                response.Items = items;
                response.Success = true;
                response.Message = "Personal lookup data retrieved successfully.";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred: " + ex.Message + " " + ex.InnerException + " " + ex.StackTrace;
            }

            return response;
        }
    }
}
