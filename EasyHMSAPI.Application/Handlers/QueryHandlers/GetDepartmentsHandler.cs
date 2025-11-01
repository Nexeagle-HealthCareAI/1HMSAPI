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
    public class GetDepartmentsHandler : IRequestHandler<GetDepartmentsRequestModel, GetDepartmentsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetDepartmentsHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<GetDepartmentsResponseModel> Handle(GetDepartmentsRequestModel request, CancellationToken cancellationToken)
        {
            var departments = await _context.Departments
                .Where(d => d.HospitalID == null || d.HospitalID == request.HospitalId)
                .Select(d => new DepartmentInfo
                {
                    DepartmentID = d.DepartmentID,
                    HospitalID = d.HospitalID,
                    Name = d.Name,
                    Description = d.Description,
                    IsActive = d.IsActive
                }).ToListAsync(cancellationToken);

            return new GetDepartmentsResponseModel { Departments = departments };
        }
    }
}
