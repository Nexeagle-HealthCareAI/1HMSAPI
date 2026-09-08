using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHrEmployeesHandler : IRequestHandler<GetHrEmployeesRequestModel, GetHrEmployeesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrEmployeesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrEmployeesResponseModel> Handle(GetHrEmployeesRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.HrEmployee
                .Include(e => e.Department)
                .Where(e => e.HospitalId == request.HospitalId);

            if (!string.IsNullOrEmpty(request.DepartmentId) && Guid.TryParse(request.DepartmentId, out Guid deptId))
            {
                query = query.Where(e => e.DepartmentId == deptId);
            }

            if (!string.IsNullOrEmpty(request.EmploymentType))
            {
                query = query.Where(e => e.EmploymentType == request.EmploymentType);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var employees = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(e => new HrEmployeeDto
                {
                    HrEmployeeId = e.HrEmployeeId,
                    EmployeeCode = e.EmployeeCode,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Gender = e.Gender,
                    ContactNumber = e.ContactNumber,
                    Email = e.Email,
                    EmploymentType = e.EmploymentType,
                    Designation = e.Designation,
                    DepartmentName = e.Department != null ? e.Department.Name : "N/A",
                    DateOfJoining = e.DateOfJoining,
                    Status = e.Status
                })
                .ToListAsync(cancellationToken);

            return new GetHrEmployeesResponseModel
            {
                Success = true,
                Message = "Employees retrieved successfully",
                Employees = employees,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}
