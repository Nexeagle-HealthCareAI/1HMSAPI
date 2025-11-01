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
