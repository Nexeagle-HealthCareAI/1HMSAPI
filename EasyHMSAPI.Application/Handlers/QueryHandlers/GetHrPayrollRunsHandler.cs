using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHrPayrollRunsHandler : IRequestHandler<GetHrPayrollRunsRequestModel, GetHrPayrollRunsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrPayrollRunsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrPayrollRunsResponseModel> Handle(GetHrPayrollRunsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.HrPayrollRun.Where(p => p.HospitalId == request.HospitalId);

            if (request.Month.HasValue)
            {
                query = query.Where(p => p.Month == request.Month.Value);
            }

            if (request.Year.HasValue)
            {
                query = query.Where(p => p.Year == request.Year.Value);
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(p => p.Status == request.Status);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var runs = await query
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new HrPayrollRunDto
                {
                    HrPayrollRunId = p.HrPayrollRunId,
                    HospitalId = p.HospitalId,
                    Month = p.Month,
                    Year = p.Year,
                    Status = p.Status,
                    TotalGrossDisbursement = p.TotalGrossDisbursement,
                    TotalNetDisbursement = p.TotalNetDisbursement,
                    TotalPfDeducted = p.TotalPfDeducted,
                    TotalEsiDeducted = p.TotalEsiDeducted,
                    TotalTdsDeducted = p.TotalTdsDeducted,
                    ProcessedByUserId = p.ProcessedByUserId,
                    ProcessedAt = p.ProcessedAt
                })
                .ToListAsync(cancellationToken);

            return new GetHrPayrollRunsResponseModel
            {
                Success = true,
                TotalCount = totalCount,
                PayrollRuns = runs
            };
        }
    }
}
