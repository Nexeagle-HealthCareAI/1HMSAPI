using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetInstrumentSetsHandler : IRequestHandler<GetInstrumentSetsRequestModel, GetInstrumentSetsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetInstrumentSetsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetInstrumentSetsResponseModel> Handle(GetInstrumentSetsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.InstrumentSet.Where(s => s.HospitalId == request.HospitalId && s.IsActive);

            if (!string.IsNullOrWhiteSpace(request.Status))
                query = query.Where(s => s.CurrentStatus == request.Status.Trim().ToUpperInvariant());

            var sets = await query
                .OrderBy(s => s.SetCode)
                .Select(s => new InstrumentSetDataModel
                {
                    InstrumentSetId = s.InstrumentSetId,
                    SetCode = s.SetCode,
                    SetName = s.SetName,
                    Category = s.Category,
                    CurrentStatus = s.CurrentStatus,
                    CurrentLocation = s.CurrentLocation,
                    IsActive = s.IsActive,
                })
                .ToListAsync(cancellationToken);

            return new GetInstrumentSetsResponseModel { Sets = sets };
        }
    }
}
