using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorShiftConfigHandler : IRequestHandler<DoctorShiftConfigRequestModel, DoctorShiftConfigResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorShiftConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorShiftConfigResponseModel> Handle(DoctorShiftConfigRequestModel request, CancellationToken cancellationToken)
        {
            var doctorExists = await (from d in _context.Doctors
                join u in _context.Users on d.UserID equals u.UserID
                where d.DoctorID == request.DoctorId && u.UserStatusId != (int)UserStatusEnum.Revoked
                select new { d.DoctorID, d.UserID }).FirstOrDefaultAsync(cancellationToken);

            if (doctorExists == null)
                return null!;

            var daysCount = request.DaysCount.GetValueOrDefault(1);
            if (daysCount <= 0) daysCount = 1;

            var calculatedEndDate = request.StartDate.AddDays(daysCount - 1);

            var response = new DoctorShiftConfigResponseModel
            {
                DoctorId = doctorExists.DoctorID,
                StartDate = DateOnly.FromDateTime(request.StartDate),
                EndDate = DateOnly.FromDateTime(calculatedEndDate),
                ShiftInfo = new List<ShiftInfo>()
            };

            var doctorOverrideShifts = await _context.DoctorShiftOverrides
                .Where(x => x.DoctorID == request.DoctorId &&
                            x.StartDate.HasValue && x.EndDate.HasValue &&
                            DateOnly.FromDateTime(x.StartDate.Value) <= response.EndDate &&
                            DateOnly.FromDateTime(x.EndDate.Value) >= response.StartDate)
                .ToListAsync(cancellationToken);

            var doctorDefaultShifts = await _context.DoctorShiftTemplates
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            for (var date = response.StartDate; date <= response.EndDate; date = date.AddDays(1))
            {
                var dayInfo = new ShiftInfo
                {
                    ShiftDate = date,
                    ShiftDayDetails = new List<ShiftDayDetails>()
                };

                var overrideShiftsForDay = doctorOverrideShifts
                    .Where(item =>
                        DateOnly.FromDateTime(item.StartDate!.Value) <= date &&
                        DateOnly.FromDateTime(item.EndDate!.Value) >= date)
                    .ToList();

                if (overrideShiftsForDay.Any())
                {
                    dayInfo.DataSource = AppConstants.ShiftDataSource_Override;
                    foreach (var item in overrideShiftsForDay)
                    {
                        dayInfo.ShiftDayDetails.Add(new ShiftDayDetails
                        {
                            OverrideId = item.OverrideID,
                            ShiftName = item.ShiftName,
                            StartTime = item.StartTime,
                            EndTime = item.EndTime,
                            SlotDurationInMinutes = item.SlotDurationInMinutes,
                            RecurringDays = item.RecurringDays
                        });
                    }
                }
                else
                {
                    dayInfo.DataSource = AppConstants.ShiftDataSource_Default;
                    foreach (var item in doctorDefaultShifts)
                    {
                        dayInfo.ShiftDayDetails.Add(new ShiftDayDetails
                        {
                            ShiftName = item.ShiftName,
                            StartTime = item.StartTime,
                            EndTime = item.EndTime,
                            SlotDurationInMinutes = item.SlotDurationInMinutes
                        });
                    }
                }

                response.ShiftInfo.Add(dayInfo);
            }

            return response;
        }
    }
}
