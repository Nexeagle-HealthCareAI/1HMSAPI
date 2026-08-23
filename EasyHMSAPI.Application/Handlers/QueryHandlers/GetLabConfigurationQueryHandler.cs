using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetLabConfigurationQueryHandler : IRequestHandler<GetLabConfigurationQuery, LabConfiguration>
    {
        private readonly AppDbContext _context;

        public GetLabConfigurationQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<LabConfiguration> Handle(GetLabConfigurationQuery request, CancellationToken cancellationToken)
        {
            var config = await _context.LabConfiguration
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalId == request.HospitalId, cancellationToken);
            
            if (config == null)
            {
                // Return default if not exists
                return new LabConfiguration
                {
                    HospitalId = request.HospitalId
                };
            }

            return config;
        }
    }
}
