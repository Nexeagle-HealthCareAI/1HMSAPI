using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorTimeOffListHandler : IRequestHandler<DoctorTimeOffListRequestModel, DoctorTimeOffListResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorTimeOffListHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorTimeOffListResponseModel> Handle(DoctorTimeOffListRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new DoctorTimeOffListResponseModel { DoctorId = request.DoctorId };
            var today = DateTime.UtcNow.Date;

            var items = await _context.DoctorTimeOffs
                .AsNoTracking()
                .Where(t => t.DoctorID == request.DoctorId && t.HospitalId == request.HospitalId)
                .OrderByDescending(t => t.FromDate)
                .ToListAsync(cancellationToken);

            foreach (var t in items)
            {
                resp.TimeOffs.Add(new DoctorTimeOffItem
                {
                    TimeOffId = t.TimeOffID,
                    FromDate = t.FromDate,
                    ToDate = t.ToDate,
                    Reason = t.Reason,
                    IsUpcoming = t.ToDate >= today,
                    CreatedAt = t.CreatedAt
                });
            }
            return resp;
        }
    }
}
