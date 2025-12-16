using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

            var existingDoctor = await _dbContext.Doctors
                .Where(x => x.DoctorID == request.DoctorId)
                .FirstOrDefaultAsync(cancellationToken);
            if(existingDoctor == null)
            {
                return new UpsertPersonalizedDataResponseModel { Message = "Doctor not found." };
            }

            var existingHospital = await _dbContext.Hospitals
                .Where(x => x.HospitalID == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingHospital == null)
            {
                return new UpsertPersonalizedDataResponseModel { Message = "Hospital not found." };
            }

            var existing = await _dbContext.LookupPersonals
                .FirstOrDefaultAsync(lp => lp.DoctorID == request.DoctorId && lp.LookupTypeId == lookupType.LookupTypeId && lp.Name == request.Data.Name, cancellationToken);

            if (existing != null)
            {
                existing.Code = request.Data.Code;
                existing.ShortDesc = request.Data.ShortDesc;
                existing.MetaJson = string.IsNullOrWhiteSpace(request.Data.Synonyms) ? null : request.Data.Synonyms;
                existing.ModifiedAt = DateTime.UtcNow;
                _dbContext.LookupPersonals.Update(existing);
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
                MetaJson = string.IsNullOrWhiteSpace(request.Data.Synonyms) ? null : request.Data.Synonyms,
                IsActive = true,
                IsOverride = true,
                HideMaster = false,
                UsageCount = 0,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            await _dbContext.LookupPersonals.AddAsync(newPersonal, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new UpsertPersonalizedDataResponseModel { Message = "Success", PersonalId = newPersonal.PersonalId };
        }
    }
}