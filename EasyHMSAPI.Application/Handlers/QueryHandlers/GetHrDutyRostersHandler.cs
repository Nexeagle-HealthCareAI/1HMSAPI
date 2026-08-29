using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHrDutyRostersHandler : IRequestHandler<GetHrDutyRostersRequestModel, GetHrDutyRostersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrDutyRostersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrDutyRostersResponseModel> Handle(GetHrDutyRostersRequestModel request, CancellationToken cancellationToken)
        {
            var startDate = DateOnly.FromDateTime(request.StartDate);
            var endDate = DateOnly.FromDateTime(request.EndDate);

            var rosters = await _context.HrDutyRoster
                .Where(r => r.HospitalId == request.HospitalId && r.RosterDate >= startDate && r.RosterDate <= endDate)
                .Select(r => new HrDutyRosterDto
                {
                    HrDutyRosterId = r.HrDutyRosterId,
                    HospitalId = r.HospitalId,
                    HrEmployeeId = r.HrEmployeeId,
                    HrHospitalShiftId = r.HrHospitalShiftId,
                    RosterDate = r.RosterDate,
                    IsOnCall = r.IsOnCall,
                    WardId = r.WardId,
                    Status = r.Status,
                    RestPeriodViolation = r.RestPeriodViolation,
                    ViolationMessage = r.ViolationMessage,
                    SwappedWithRosterId = r.SwappedWithRosterId,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new GetHrDutyRostersResponseModel
            {
                Success = true,
                Rosters = rosters
            };
        }
    }
}
