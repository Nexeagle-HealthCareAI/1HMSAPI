using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class ToggleDepartmentStatusHandler : IRequestHandler<ToggleDepartmentStatusRequestModel, ToggleDepartmentStatusResponseModel>
    {
        private readonly AppDbContext _context;
        public ToggleDepartmentStatusHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<ToggleDepartmentStatusResponseModel> Handle(ToggleDepartmentStatusRequestModel request, CancellationToken cancellationToken)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentID == request.DepartmentId, cancellationToken);
            if (department == null)
            {
                return new ToggleDepartmentStatusResponseModel
                {
                    DepartmentID = request.DepartmentId,
                    IsActive = false,
                    Message = "Department not found."
                };
            }
            // Global master departments (no hospital) are shared by every hospital: only the platform
            // may change them. Hospital departments need admin_panel at the owning hospital.
            if (department.HospitalID == null
                || request.CallerUserId == null
                || !await Common.CallerGuards.HasPermissionAtHospitalAsync(_context, request.CallerUserId.Value, department.HospitalID.Value, "admin_panel", cancellationToken))
            {
                return new ToggleDepartmentStatusResponseModel
                {
                    DepartmentID = request.DepartmentId,
                    IsActive = department.IsActive,
                    Forbidden = true,
                    Message = "You don't have permission to change this department."
                };
            }
            department.IsActive = !department.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            return new ToggleDepartmentStatusResponseModel
            {
                DepartmentID = department.DepartmentID,
                IsActive = department.IsActive,
                Message = department.IsActive ? "Department enabled." : "Department disabled."
            };
        }
    }
}
