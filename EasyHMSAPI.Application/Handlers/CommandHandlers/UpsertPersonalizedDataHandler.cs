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
        public UpsertPersonalizedDataHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UpsertPersonalizedDataResponseModel> Handle(UpsertPersonalizedDataRequestModel request, CancellationToken cancellationToken)
        {
            var lookupType = await _dbContext.LookupTypes
                .FirstOrDefaultAsync(lt => lt.LookupTypeCode == request.LookupType, cancellationToken);
            if (lookupType == null)
            {
                return new UpsertPersonalizedDataResponseModel { Message = "Lookup type not found." };
            }

            // Normalize MetaJson to satisfy CHECK constraint: (MetaJson IS NULL OR ISJSON(MetaJson) = 1)
            string? metaJson = NormalizeToJsonOrNull(request.Data.Synonyms);

            // If PersonalId is provided, update the record; otherwise insert a new record
            if (!string.IsNullOrWhiteSpace(request.Data.PersonalId) && Guid.TryParse(request.Data.PersonalId, out var personalId) && personalId != Guid.Empty)
            {
                var existing = await _dbContext.LookupPersonals
                    .FirstOrDefaultAsync(lp => lp.PersonalId == personalId
                                               && lp.DoctorID == request.DoctorId
                                               && lp.HospitalID == request.HospitalId
                                               && lp.LookupTypeId == lookupType.LookupTypeId,
                                               cancellationToken);

                if (existing == null)
                {
                    return new UpsertPersonalizedDataResponseModel { Message = "Personalized data not found." };
                }

                existing.Code = request.Data.Code;
                existing.Name = request.Data.Name;
                existing.ShortDesc = request.Data.ShortDesc;
                existing.MetaJson = metaJson;
                existing.IsActive = true;
                existing.IsOverride = true;
                existing.ModifiedAt = DateTime.UtcNow;                
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new UpsertPersonalizedDataResponseModel { Message = "Success", PersonalId = existing.PersonalId };
            }

            var masterLookup = await _dbContext.LookupMasters
                .FirstOrDefaultAsync(lm => lm.LookupTypeId == lookupType.LookupTypeId, cancellationToken);

            if (masterLookup == null)
            {
                return new UpsertPersonalizedDataResponseModel { Message = "Master lookup not found for the given name and lookup type." };
            }

            var newPersonal = new LookupPersonal
            {
                PersonalId = Guid.NewGuid(),
                HospitalID = request.HospitalId,
                DoctorID = request.DoctorId,
                MasterLookupId = masterLookup.LookupId,
                LookupTypeId = lookupType.LookupTypeId,
                Code = request.Data.Code,
                Name = request.Data.Name,
                ShortDesc = request.Data.ShortDesc,
                MetaJson = metaJson,
                IsActive = true,
                IsOverride = false,
                HideMaster = false,
                UsageCount = 0,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt= DateTime.UtcNow               
            };

            await _dbContext.LookupPersonals.AddAsync(newPersonal, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new UpsertPersonalizedDataResponseModel { Message = "Success", PersonalId = newPersonal.PersonalId };
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