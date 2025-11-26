using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorSlotsHandler : IRequestHandler<DoctorSlotsRequestModel, DoctorSlotsResponseModel>
    {
        private readonly AppDbContext _context;
        
        public DoctorSlotsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorSlotsResponseModel> Handle(DoctorSlotsRequestModel request, CancellationToken cancellationToken)
        {
            var response = new DoctorSlotsResponseModel
            {
                DoctorId = request.DoctorId,
                RequestedDate = request.SlotDate.Date,
                IsTimeOff = false,
                TimeOffReason = null,
                ShiftInfo = new List<ShiftInfoModel>
                {
                    new()
                    {
                        ShiftDate = DateOnly.FromDateTime(request.SlotDate.Date),
                        DataSource = "default",
                        ShiftDayDetails = new List<ShiftDayDetailsModel>()
                    }
                }
            };

            try
            {
                var doctorExists = await (from d in _context.Doctors
 join u in _context.Users on d.UserID equals u.UserID
 where d.DoctorID == request.DoctorId && u.UserStatusId != (int)UserStatusEnum.Revoked
 select d.DoctorID).AnyAsync(cancellationToken);

                if (!doctorExists)
                {
                    throw new KeyNotFoundException($"Doctor with ID {request.DoctorId} not found");
                }

                var timeOff = await _context.DoctorTimeOffs
                    .Where(to => to.DoctorID == request.DoctorId &&
                               to.HospitalId == request.HospitalId &&
                               request.SlotDate.Date >= to.FromDate.Date &&
                               request.SlotDate.Date <= to.ToDate.Date)
                    .OrderByDescending(to => to.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (timeOff != null)
                {
                    response.IsTimeOff = true;
                    response.TimeOffReason = timeOff.Reason;
                    response.ShiftInfo = null;
                    return response;
                }

                var requestDate = request.SlotDate.Date;
                
                var overrideShifts = await _context.DoctorShiftOverrides
                    .Where(o => o.DoctorID == request.DoctorId &&
                              o.HospitalId == request.HospitalId &&
                              o.StartDate <= requestDate && 
                              (!o.EndDate.HasValue || o.EndDate >= requestDate))
                    .OrderBy(o => o.StartTime)
                    .ToListAsync(cancellationToken);

                if (overrideShifts.Count > 0)
                {
                    response.ShiftInfo[0].DataSource = AppConstants.ShiftDataSource_Override;
                    response.ShiftInfo[0].ShiftDayDetails = overrideShifts
                        .Select(shift => new ShiftDayDetailsModel
                        {
                            OverrideId = shift.OverrideID,
                            ShiftName = shift.ShiftName,
                            StartTime = shift.StartTime,
                            EndTime = shift.EndTime,
                            SlotDurationInMinutes = shift.SlotDurationInMinutes,
                            RecurringDays = shift.RecurringDays
                        })
                        .ToList();
                }
                else
                {
                    response.ShiftInfo[0].DataSource = AppConstants.ShiftDataSource_Default;
                    response.ShiftInfo[0].ShiftDayDetails = await _context.DoctorShiftTemplates
                        .Where(t => t.IsActive && t.HospitalId == request.HospitalId)
                        .OrderBy(t => t.StartTime)
                        .Select(t => new ShiftDayDetailsModel
                        {
                            ShiftName = t.ShiftName,
                            StartTime = t.StartTime,
                            EndTime = t.EndTime,
                            SlotDurationInMinutes = t.SlotDurationInMinutes
                        })
                        .ToListAsync(cancellationToken);
                }

                return response;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while fetching doctor's schedule.", ex);
            }
        }
    }
}
