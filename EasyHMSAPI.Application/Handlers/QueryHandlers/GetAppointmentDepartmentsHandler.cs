using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAppointmentDepartmentsHandler : IRequestHandler<GetAppointmentDepartmentsRequestModel, GetAppointmentDepartmentsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetAppointmentDepartmentsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAppointmentDepartmentsResponseModel> Handle(GetAppointmentDepartmentsRequestModel request, CancellationToken cancellationToken)
        {
            var departments = await _context.HospitalDepartmentMappings
                .Include(m => m.Department)
                .Where(m => m.HospitalID == request.HospitalId)
                .Select(m => new AppointmentDepartmentInfo
                {
                    DepartmentId = m.DepartmentID,
                    DepartmentName = m.Department.Name
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            return new GetAppointmentDepartmentsResponseModel { Departments = departments };
        }
    }
}
