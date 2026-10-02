using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateDepartmentHandler : IRequestHandler<UpdateDepartmentRequestModel, UpdateDepartmentResponseModel>
    {
        private readonly AppDbContext _context;
        public UpdateDepartmentHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<UpdateDepartmentResponseModel> Handle(UpdateDepartmentRequestModel request, CancellationToken cancellationToken)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentID == request.DepartmentId, cancellationToken);
            if (department == null)
            {
                return new UpdateDepartmentResponseModel
                {
                    DepartmentID = request.DepartmentId,
                    Success = false,
                    Message = "Department not found."
                };
            }
            // Global master departments (no hospital) are shared by every hospital: only the platform
            // may change them. Hospital departments need admin_panel at the owning hospital.
            if (department.HospitalID == null
                || request.CallerUserId == null
                || !await Common.CallerGuards.HasPermissionAtHospitalAsync(_context, request.CallerUserId.Value, department.HospitalID.Value, "admin_panel", cancellationToken))
            {
                return new UpdateDepartmentResponseModel
                {
                    DepartmentID = request.DepartmentId,
                    Success = false,
                    Forbidden = true,
                    Message = "You don't have permission to change this department."
                };
            }

            department.Name = request.Name;
            department.Description = request.Description;
            await _context.SaveChangesAsync(cancellationToken);
            return new UpdateDepartmentResponseModel
            {
                DepartmentID = department.DepartmentID,
                Name = department.Name,
                Description = department.Description,
                Success = true,
                Message = "Department updated successfully."
            };
        }
    }
}
