using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorSpecializationsHandler : IRequestHandler<DoctorSpecializationsRequestModel, DoctorSpecializationsResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorSpecializationsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorSpecializationsResponseModel> Handle(DoctorSpecializationsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Specializations.AsQueryable();
            query = query.Where(s => s.DepartmentID == request.DepartmentId && s.IsActive);

            if (request.HospitalId.HasValue)
            {
                if (request.IncludeGlobal)
                {
                    query = query.Where(s => s.HospitalID == null || s.HospitalID == request.HospitalId);
                }
                else
                {
                    query = query.Where(s => s.HospitalID == request.HospitalId);
                }
            }
            else
            {
                query = query.Where(s => s.HospitalID == null);
            }

            var items = await query
                .OrderBy(s => s.Name)
                .Select(s => new SpecializationItem
                {
                    SpecializationId = s.SpecializationID,
                    Name = s.Name,
                    Description = s.Description
                })
                .ToListAsync(cancellationToken);

            return new DoctorSpecializationsResponseModel
            {
                DepartmentId = request.DepartmentId,
                HospitalId = request.HospitalId,
                IncludeGlobal = request.IncludeGlobal,
                Items = items
            };
        }
    }
}
