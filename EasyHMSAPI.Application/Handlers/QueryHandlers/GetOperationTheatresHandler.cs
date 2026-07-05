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
            var query = _context.OperationTheatre.Where(t => t.HospitalId == request.HospitalId);
            if (!request.IncludeInactive)
                query = query.Where(t => t.IsActive);

            var theatres = await (
                    from t in query
                    join d in _context.Departments on t.DepartmentId equals d.DepartmentID into dj
                    from d in dj.DefaultIfEmpty()
                    orderby t.TheatreCode
                    select new OperationTheatreDataModel
                    {
                        TheatreId = t.TheatreId,
                        TheatreCode = t.TheatreCode,
                        TheatreName = t.TheatreName,
                        Status = t.Status,
                        IsActive = t.IsActive,
                        DepartmentId = t.DepartmentId,
                        DepartmentName = d != null ? d.Name : null,
                        Price = t.Price,
                    })
                .ToListAsync(cancellationToken);

            return new GetOperationTheatresResponseModel { Theatres = theatres };
        }
    }
}
