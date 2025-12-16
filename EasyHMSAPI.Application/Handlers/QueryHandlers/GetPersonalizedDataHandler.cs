using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPersonalizedDataHandler : IRequestHandler<GetPersonalizedDataRequestModel, List<GetPersonalizedDataResponseModel>>
    {
        private readonly AppDbContext _dbContext;
        public GetPersonalizedDataHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<GetPersonalizedDataResponseModel>> Handle(GetPersonalizedDataRequestModel request, CancellationToken cancellationToken)
        {
            var lookupType = await _dbContext.LookupTypes.FirstOrDefaultAsync(lt => lt.LookupTypeCode == request.LookupType, cancellationToken);
            if (lookupType == null) return new List<GetPersonalizedDataResponseModel>();

            var data = await _dbContext.LookupPersonals
                .AsNoTracking()
                .Where(lp => lp.DoctorID == request.DoctorId && lp.LookupTypeId == lookupType.LookupTypeId && lp.IsActive)
                .Select(lp => new GetPersonalizedDataResponseModel
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

            return data;
        }
    }
}