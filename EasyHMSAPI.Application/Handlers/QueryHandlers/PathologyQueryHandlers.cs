using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPathologyOrdersHandler : IRequestHandler<GetPathologyOrdersQuery, List<PathologyOrderDto>>
    {
        private readonly AppDbContext _context;

        public GetPathologyOrdersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PathologyOrderDto>> Handle(GetPathologyOrdersQuery request, CancellationToken cancellationToken)
        {
            var query = _context.PathologyOrder
                .Where(o => o.HospitalId == request.HospitalId);

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(o => o.Status == request.Status);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new PathologyOrderDto
                {
                    OrderId = o.OrderId,
                    OrderNo = o.OrderNo,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    PatientId = o.PatientId,
                    // Get patient name if possible, assuming PatientRegistration is joined
                    PatientName = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId)
                        .Select(p => p.FullName)
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync(cancellationToken);

            return orders;
        }
    }

    public class GetPathologyOrderByIdHandler : IRequestHandler<GetPathologyOrderByIdQuery, PathologyOrderDto>
    {
        private readonly AppDbContext _context;

        public GetPathologyOrderByIdHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PathologyOrderDto> Handle(GetPathologyOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var order = await _context.PathologyOrder
                .Where(o => o.HospitalId == request.HospitalId && o.OrderId == request.OrderId)
                .Select(o => new PathologyOrderDto
                {
                    OrderId = o.OrderId,
                    OrderNo = o.OrderNo,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    PatientId = o.PatientId,
                    PatientName = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId)
                        .Select(p => p.FullName)
                        .FirstOrDefault() ?? "Unknown"
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null) return new PathologyOrderDto();

            var lines = await _context.PathologyOrderLine
                .Where(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId)
                .ToListAsync(cancellationToken);

            foreach (var line in lines)
            {
                var test = await _context.PathologyTestMaster
                    .Where(t => t.TestId == line.TestId)
                    .FirstOrDefaultAsync(cancellationToken);
                    
                var result = await _context.PathologyResult
                    .Where(r => r.OrderLineId == line.OrderLineId)
                    .FirstOrDefaultAsync(cancellationToken);

                order.Lines.Add(new PathologyOrderLineDto
                {
                    OrderLineId = line.OrderLineId,
                    TestId = line.TestId,
                    TestName = test?.TestName ?? "Unknown Test",
                    TestCode = test?.TestCode ?? "Unknown Code",
                    Status = line.Status,
                    ParameterSchemaJson = test?.ParameterSchemaJson,
                    Result = result == null ? null : new PathologyResultDto
                    {
                        ResultId = result.ResultId,
                        ResultValuesJson = result.ResultValuesJson,
                        Interpretation = result.Interpretation
                    }
                });
            }

            return order;
        }
    }
}
