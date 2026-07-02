using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetOperationTheatresHandler : IRequestHandler<GetOperationTheatresRequestModel, GetOperationTheatresResponseModel>
    {
        private readonly AppDbContext _context;

        public GetOperationTheatresHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetOperationTheatresResponseModel> Handle(GetOperationTheatresRequestModel request, CancellationToken cancellationToken)
        {
            var theatres = await _context.OperationTheatre
                .Where(t => t.HospitalId == request.HospitalId && t.IsActive)
                .OrderBy(t => t.TheatreCode)
                .Select(t => new OperationTheatreDataModel
                {
                    TheatreId = t.TheatreId,
                    TheatreCode = t.TheatreCode,
                    TheatreName = t.TheatreName,
                    Status = t.Status,
                    IsActive = t.IsActive,
                })
                .ToListAsync(cancellationToken);

            return new GetOperationTheatresResponseModel { Theatres = theatres };
        }
    }
}
