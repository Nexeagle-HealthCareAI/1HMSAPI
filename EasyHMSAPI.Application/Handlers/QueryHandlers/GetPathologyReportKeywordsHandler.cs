using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPathologyReportKeywordsHandler : IRequestHandler<GetPathologyReportKeywordsRequestModel, GetPathologyReportKeywordsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPathologyReportKeywordsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPathologyReportKeywordsResponseModel> Handle(GetPathologyReportKeywordsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.PathologyReportKeyword.Where(k => k.HospitalId == request.HospitalId);

            // No TestId -> management "list everything" view. A real TestId -> that test's own
            // keywords plus every global (TestId IS NULL) keyword, since both are usable while
            // reporting on that specific test.
            if (request.TestId.HasValue)
                query = query.Where(k => k.TestId == request.TestId.Value || k.TestId == null);

            if (!request.IncludeInactive)
                query = query.Where(k => k.IsActive);

            var rows = await query.OrderBy(k => k.Keyword).ToListAsync(cancellationToken);

            var testIds = rows.Where(r => r.TestId.HasValue).Select(r => r.TestId!.Value).Distinct().ToList();
            var testNames = testIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.PathologyTestMaster
                    .Where(t => t.HospitalId == request.HospitalId && testIds.Contains(t.TestId))
                    .ToDictionaryAsync(t => t.TestId, t => t.TestName, cancellationToken);

            var keywords = rows.Select(k => new PathologyReportKeywordDataModel
            {
                KeywordId = k.KeywordId,
                TestId = k.TestId,
                TestName = k.TestId.HasValue && testNames.TryGetValue(k.TestId.Value, out var name) ? name : null,
                Keyword = k.Keyword,
                ContentJson = k.ContentJson,
                IsActive = k.IsActive,
            }).ToList();

            return new GetPathologyReportKeywordsResponseModel { Keywords = keywords };
        }
    }
}
