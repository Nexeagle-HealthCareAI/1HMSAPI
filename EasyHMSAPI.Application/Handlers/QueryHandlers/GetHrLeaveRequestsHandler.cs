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
    public class GetHrLeaveRequestsHandler : IRequestHandler<GetHrLeaveRequestsRequestModel, GetHrLeaveRequestsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrLeaveRequestsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrLeaveRequestsResponseModel> Handle(GetHrLeaveRequestsRequestModel request, CancellationToken cancellationToken)
        {
            // RBAC Check for Self-Service Isolation
            var hasManageLeaves = await _context.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .AnyAsync(ur => ur.UserID == request.LoggedInUserId &&
                                ur.Role.RolePermissions.Any(p => p.PermissionKey == "hr.manage_leaves" && p.IsAllowed), cancellationToken);

            var query = _context.HrLeaveRequest
                .Include(l => l.HrEmployee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            if (!hasManageLeaves)
            {
                // Force isolation: user can only see leaves linked to their own identity
                query = query.Where(l => l.HrEmployee.UserId == request.LoggedInUserId);
            }
            else if (request.EmployeeId.HasValue)
            {
                query = query.Where(l => l.HrEmployeeId == request.EmployeeId.Value);
            }

            if (request.HospitalId.HasValue)
            {
                query = query.Where(l => l.HrEmployee.HospitalId == request.HospitalId.Value);
            }
            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(l => l.Status == request.Status);
            }

            var results = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new HrLeaveRequestDto
                {
                    Id = l.HrLeaveRequestId,
                    EmployeeId = l.HrEmployeeId,
                    EmployeeName = l.HrEmployee.FirstName + " " + l.HrEmployee.LastName,
                    EmployeeCode = l.HrEmployee.EmployeeCode,
                    DepartmentName = l.HrEmployee.Department != null ? l.HrEmployee.Department.Name : "N/A",
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    Reason = l.Reason,
                    Status = l.Status,
                    ApprovedById = l.ApprovedByUserId,
                    ApprovedAt = l.ApprovedAt,
                    MedicalCertificateUrl = l.MedicalCertificateUrl,
                    RejectionReason = l.RejectionReason,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new GetHrLeaveRequestsResponseModel
            {
                Success = true,
                LeaveRequests = results
            };
        }
    }
}
