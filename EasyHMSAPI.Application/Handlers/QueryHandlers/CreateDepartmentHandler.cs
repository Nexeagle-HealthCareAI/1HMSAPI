using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class CreateDepartmentHandler : IRequestHandler<CreateDepartmentRequestModel, CreateDepartmentResponseModel>
    {
        private readonly AppDbContext _context;
        public CreateDepartmentHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<CreateDepartmentResponseModel> Handle(CreateDepartmentRequestModel request, CancellationToken cancellationToken)
        {
            var department = new Department
            {
                DepartmentID = Guid.NewGuid(),
                HospitalID = request.HospitalID,
                Name = request.Name,
                Description = request.Description,
                CreatedByUserID = request.CreatedByUserID,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            await _context.Departments.AddAsync(department, cancellationToken);

            var mapping = new HospitalDepartmentMapping
            {
                MappingID = Guid.NewGuid(),
                HospitalID = request.HospitalID,
                DepartmentID = department.DepartmentID,
                IsActive = true,
                MappedAt = DateTime.UtcNow
            };
            await _context.HospitalDepartmentMappings.AddAsync(mapping, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return new CreateDepartmentResponseModel
            {
                DepartmentID = department.DepartmentID,
                HospitalID = department.HospitalID ?? Guid.Empty,
                Name = department.Name,
                Description = department.Description,
                IsActive = department.IsActive,
                Message = "Department created successfully."
            };
        }
    }
}
