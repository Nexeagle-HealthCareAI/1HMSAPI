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
    public class GetPathologyReportTemplatesQueryHandler : IRequestHandler<GetPathologyReportTemplatesQuery, List<PathologyReportTemplate>>
    {
        private readonly AppDbContext _context;

        public GetPathologyReportTemplatesQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PathologyReportTemplate>> Handle(GetPathologyReportTemplatesQuery request, CancellationToken cancellationToken)
        {
            return await _context.PathologyReportTemplate
                .Where(x => x.HospitalId == request.HospitalId)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.TemplateName)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
