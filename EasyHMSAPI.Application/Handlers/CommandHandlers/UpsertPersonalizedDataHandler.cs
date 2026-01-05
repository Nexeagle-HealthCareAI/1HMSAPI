using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPersonalizedDataHandler : IRequestHandler<UpsertPersonalizedDataRequestModel, UpsertPersonalizedDataResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public UpsertPersonalizedDataHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<UpsertPersonalizedDataResponseModel> Handle(UpsertPersonalizedDataRequestModel request, CancellationToken cancellationToken)
        {
            UpsertPersonalizedDataResponseModel response = new();
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
                    .Where(lt => lt.LookupTypeCode.ToUpper() == lookupTypeUpper)
                    .Select(x => new
                    {
                        x.LookupTypeId,
                        x.LookupTypeCode
                    }).FirstOrDefaultAsync(cancellationToken);
                if (lookupType is not null)
                {
                    string? metaJson = NormalizeToJsonOrNull(request.Data.Synonyms);
                    if (!string.IsNullOrWhiteSpace(request.Data.PersonalId) && Guid.TryParse(request.Data.PersonalId, out var personalId) && personalId != Guid.Empty)
                    {
                        var existingLookup = await _dbContext.LookupPersonals
                            .FirstOrDefaultAsync(lp => lp.PersonalId == personalId
                                                       && lp.DoctorID == request.DoctorId
                                                       && lp.HospitalID == request.HospitalId
                                                       && lp.LookupTypeId == lookupType.LookupTypeId,
                                                       cancellationToken);
                        if (existingLookup == null)
                        {
                            response.Success = false;
                            response.Message = "Personalized data not found.";
                        }
                        else
                        {
                            if(!string.IsNullOrEmpty(request.Data.Code))existingLookup.Code = request.Data.Code.ToUpper();
                            if(!string.IsNullOrEmpty(request.Data.Name))existingLookup.Name = request.Data.Name;
                            if (!string.IsNullOrEmpty(request.Data.ShortDesc)) existingLookup.ShortDesc = request.Data.ShortDesc;
                            if (!string.IsNullOrEmpty(metaJson)) existingLookup.MetaJson = metaJson;
                            existingLookup.IsActive = true;
                            existingLookup.IsOverride = true;
                            existingLookup.ModifiedAt = DateTime.UtcNow;
                            existingLookup.ModifiedBy = request.LoggedInUserId;
                           
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            response.Success = true;
                            response.Message = "Personalized data updated";
                            response.PersonalId = existingLookup.PersonalId;
                        }
                    }
                    else if (!string.IsNullOrEmpty(request.Source))
                    {
                        if (request.Source.ToLower() == "prescription")
                        {
                            var name = request?.Data?.Name?.Trim().ToLower();
                            var existingLookup = await _dbContext.LookupPersonals
                                .Where(x => x.Name != null 
                                       && x.Name.Trim().ToLower() == name 
                                       && request != null 
                                       && x.DoctorID == request.DoctorId
                                       && x.HospitalID == request.HospitalId)
                                .FirstOrDefaultAsync(cancellationToken);
                            if (existingLookup != null)
                            {
                                existingLookup.UsageCount += 1;

                                response.Success = true;
                                response.PersonalId = existingLookup.PersonalId;
                                response.Message = "Personalized data usage count updated";
                            }
                            else
                            {
                                response.Success = false;
                                response.Message = "Personalized data not found for usage count updated.";
                            }
                        }
                    }
                    else
                    {
                        var masterLookup = await _dbContext.LookupMasters
                            .FirstOrDefaultAsync(lm => lm.LookupTypeId == lookupType.LookupTypeId, cancellationToken);
                        var newPersonal = new LookupPersonal
                        {
                            PersonalId = Guid.NewGuid(),
                            HospitalID = request.HospitalId,
                            DoctorID = request.DoctorId,
                            MasterLookupId = masterLookup?.LookupId,
                            LookupTypeId = lookupType.LookupTypeId,
                            Code = request.Data?.Code?.ToUpper(),
                            Name = request.Data?.Name ?? string.Empty,
                            ShortDesc = request.Data?.ShortDesc,
                            MetaJson = metaJson,
                            IsActive = true,
                            IsOverride = false,
                            HideMaster = false,
                            UsageCount = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = request.LoggedInUserId,
                            ModifiedAt = DateTime.UtcNow,
                            ModifiedBy = request.LoggedInUserId
                        };

                        await _dbContext.LookupPersonals.AddAsync(newPersonal, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        response.PersonalId = newPersonal.PersonalId;
                        response.Success = true;
                        response.Message = "Personalized data added";
                    }
                }
                else
                {
                    response.Success = false;
                    response.Message = "Lookup type not found.";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "Error: " + ex.Message + ex.InnerException + ex.StackTrace;
            }
            
            return response;
        }

        private static string? NormalizeToJsonOrNull(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // If already valid JSON, keep as-is
            if (IsValidJson(input)) return input;

            // Otherwise, treat as comma-separated synonyms and serialize to JSON array
            var items = input
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (items.Length == 0) return null;
            return JsonSerializer.Serialize(items);
        }

        private static bool IsValidJson(string s)
        {
            try
            {
                using var _ = JsonDocument.Parse(s);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}