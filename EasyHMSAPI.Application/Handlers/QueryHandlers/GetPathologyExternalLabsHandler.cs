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
    public class GetPathologyExternalLabsHandler : IRequestHandler<GetPathologyExternalLabsRequestModel, GetPathologyExternalLabsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPathologyExternalLabsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPathologyExternalLabsResponseModel> Handle(GetPathologyExternalLabsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.PathologyExternalLab.Where(l => l.HospitalId == request.HospitalId);
            if (!request.IncludeInactive)
                query = query.Where(l => l.IsActive);

            var labs = await query
                .OrderBy(l => l.LabName)
                .Select(l => new PathologyExternalLabDataModel
                {
                    ExternalLabId = l.ExternalLabId,
                    LabName = l.LabName,
                    ContactPerson = l.ContactPerson,
                    Phone = l.Phone,
                    Email = l.Email,
                    Address = l.Address,
                    AccreditationNo = l.AccreditationNo,
                    IsActive = l.IsActive,
                })
                .ToListAsync(cancellationToken);

            return new GetPathologyExternalLabsResponseModel { Labs = labs };
        }
    }
}
