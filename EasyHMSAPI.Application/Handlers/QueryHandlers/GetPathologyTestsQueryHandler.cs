using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPathologyTestsQueryHandler : IRequestHandler<GetPathologyTestsQuery, List<PathologyTestMaster>>
    {
        private readonly AppDbContext _context;

        public GetPathologyTestsQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PathologyTestMaster>> Handle(GetPathologyTestsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.PathologyTestMaster
                .Where(x => x.HospitalId == request.HospitalId)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var search = request.SearchTerm.ToLower();
                query = query.Where(x => x.TestName.ToLower().Contains(search) || x.TestCode.ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(x => x.Category == request.Category);
            }

            var tests = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.TestName).ToListAsync(cancellationToken);
            if (tests.Count == 0) return tests;

            var chargeIds = tests.Where(t => t.ChargeId.HasValue).Select(t => t.ChargeId!.Value).Distinct().ToList();
            if (chargeIds.Count > 0)
            {
                var ratesByChargeId = await _context.ChargeMaster
                    .Where(c => c.HospitalId == request.HospitalId && chargeIds.Contains(c.ChargeId))
                    .ToDictionaryAsync(c => c.ChargeId, c => c.DefaultRate, cancellationToken);
                foreach (var test in tests)
                {
                    if (test.ChargeId.HasValue && ratesByChargeId.TryGetValue(test.ChargeId.Value, out var rate))
                    {
                        test.Price = rate;
                    }
                }
            }

            return tests;
        }
    }
}
