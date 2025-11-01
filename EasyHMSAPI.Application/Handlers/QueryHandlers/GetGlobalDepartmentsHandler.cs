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
    public class GetGlobalDepartmentsHandler : IRequestHandler<GetGlobalDepartmentsRequestModel, GetGlobalDepartmentsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetGlobalDepartmentsHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<GetGlobalDepartmentsResponseModel> Handle(GetGlobalDepartmentsRequestModel request, CancellationToken cancellationToken)
        {
            var departments = await _context.Departments
                .Where(d => d.HospitalID == null)
                .Select(d => new DepartmentInfo
                {
                    DepartmentID = d.DepartmentID,
                    HospitalID = d.HospitalID,
                    Name = d.Name,
                    Description = d.Description,
                    IsActive = d.IsActive
                }).ToListAsync(cancellationToken);

            return new GetGlobalDepartmentsResponseModel { Departments = departments };
        }
    }
}
