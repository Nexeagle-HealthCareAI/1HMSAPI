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
    public class GetHospitalDepartmentsHandler : IRequestHandler<GetHospitalDepartmentsRequestModel, GetHospitalDepartmentsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetHospitalDepartmentsHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<GetHospitalDepartmentsResponseModel> Handle(GetHospitalDepartmentsRequestModel request, CancellationToken cancellationToken)
        {
            var mappings = await _context.HospitalDepartmentMappings
                .Include(m => m.Department)
                .Where(m => m.HospitalID == request.HospitalId)
                .Select(m => new HospitalDepartmentInfo
                {
                    MappingID = m.MappingID,
                    HospitalID = m.HospitalID,
                    DepartmentID = m.DepartmentID,
                    DepartmentName = m.Department.Name,
                    Description = m.Department.Description,
                    IsActive = m.IsActive,
                    MappedAt = m.MappedAt
                }).ToListAsync(cancellationToken);

            return new GetHospitalDepartmentsResponseModel { Departments = mappings };
        }
    }
}
