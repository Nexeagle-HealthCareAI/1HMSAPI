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
            var query = _context.HrLeaveRequest.Include(l => l.HrEmployee).AsQueryable();

            if (request.HospitalId.HasValue)
            {
                query = query.Where(l => l.HrEmployee.HospitalId == request.HospitalId.Value);
            }
            if (request.EmployeeId.HasValue)
            {
                query = query.Where(l => l.HrEmployeeId == request.EmployeeId.Value);
            }
            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(l => l.Status == request.Status);
            }

            var results = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new HrLeaveRequestDto
                {
                    HrLeaveRequestId = l.HrLeaveRequestId,
                    HrEmployeeId = l.HrEmployeeId,
                    EmployeeName = l.HrEmployee.FirstName + " " + l.HrEmployee.LastName,
                    EmployeeCode = l.HrEmployee.EmployeeCode,
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    Reason = l.Reason,
                    Status = l.Status,
                    ApprovedByUserId = l.ApprovedByUserId,
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
