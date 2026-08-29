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
    public class GetHrHospitalShiftsHandler : IRequestHandler<GetHrHospitalShiftsRequestModel, GetHrHospitalShiftsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrHospitalShiftsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrHospitalShiftsResponseModel> Handle(GetHrHospitalShiftsRequestModel request, CancellationToken cancellationToken)
        {
            var shifts = await _context.HrHospitalShift
                .Where(s => s.HospitalId == request.HospitalId)
                .Select(s => new HrHospitalShiftDto
                {
                    HrHospitalShiftId = s.HrHospitalShiftId,
                    HospitalId = s.HospitalId,
                    ShiftCode = s.ShiftCode,
                    ShiftName = s.ShiftName,
                    StartTime = s.StartTime.ToTimeSpan(),
                    EndTime = s.EndTime.ToTimeSpan(),
                    GracePeriodMinutes = s.GracePeriodMinutes,
                    HandoverBufferMinutes = s.HandoverBufferMinutes,
                    NightAllowanceAmount = s.NightAllowanceAmount,
                    CalloutFeeAmount = s.CalloutFeeAmount,
                    IsActive = s.IsActive,
                    ApplicableRolesJson = s.ApplicableRolesJson
                })
                .ToListAsync(cancellationToken);

            return new GetHrHospitalShiftsResponseModel
            {
                Success = true,
                Shifts = shifts
            };
        }
    }
}
