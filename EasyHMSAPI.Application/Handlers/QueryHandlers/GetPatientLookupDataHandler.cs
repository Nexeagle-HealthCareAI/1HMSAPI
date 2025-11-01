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
        public GetPatientLookupDataHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GetPatientLookupDataResponseModel> Handle(GetPatientLookupDataRequestModel request, CancellationToken cancellationToken)
        {
            var lookupTypeEntity = await _dbContext.LookupTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(lt => lt.LookupTypeCode == request.LookupType, cancellationToken);

            if (lookupTypeEntity == null)
                return new GetPatientLookupDataResponseModel();

            var lookupTypeInfo = new LookupTypeInfo { Id = lookupTypeEntity.LookupTypeId, Name = lookupTypeEntity.LookupTypeCode };

            var personalQuery = _dbContext.LookupPersonals
                .AsNoTracking()
                .Where(lp => lp.LookupTypeId == lookupTypeEntity.LookupTypeId && lp.DoctorID == request.DoctorId && lp.IsActive)
                .Select(lp => new LookupItemPersonal
                {
                    PersonalId = lp.PersonalId,
                    Code = lp.Code,
                    Name = lp.Name ?? string.Empty,
                    NameLower = lp.NameLower,
                    ShortDesc = lp.ShortDesc,
                    UsageCount = lp.UsageCount
                });

            var generalQuery = _dbContext.LookupMasters
                .AsNoTracking()
                .Where(lm => lm.LookupTypeId == lookupTypeEntity.LookupTypeId && lm.IsActive)
                .Select(lm => new
                {
                    lm.Code,
                    lm.Name,
                    lm.NameLower,
                    lm.ShortDesc,
                    Synonyms = lm.Synonyms,
                    lm.UsageCount
                });

            var personalList = await personalQuery.ToListAsync(cancellationToken);
            var generalRawList = await generalQuery.ToListAsync(cancellationToken);

            var generalList = generalRawList
                .Select(lm => new LookupItemGeneral
                {
                    Code = lm.Code,
                    Name = lm.Name ?? string.Empty,
                    NameLower = lm.NameLower,
                    ShortDesc = lm.ShortDesc,
                    Synonyms = string.IsNullOrEmpty(lm.Synonyms) ? new List<string>() : lm.Synonyms.Split(';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList(),
                    UsageCount = lm.UsageCount
                })
                .ToList();

            var response = new GetPatientLookupDataResponseModel
            {
                LookupType = lookupTypeInfo,
                Scope = new ScopeInfo { HospitalId = request.HospitalId, DoctorId = request.DoctorId },
                Counts = (personalList.Count, generalList.Count),
                PersonalItems = personalList,
                GeneralItems = generalList
            };

            return response;
        }
    }
}
