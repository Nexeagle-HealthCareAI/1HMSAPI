using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Data.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorOverrideCreateHandler : IRequestHandler<DoctorOverrideCreateRequestModel, DoctorOverrideCreateResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorOverrideCreateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorOverrideCreateResponseModel> Handle(DoctorOverrideCreateRequestModel request, CancellationToken cancellationToken)
        {
            if (request.ShiftDetails != null)
            {
                foreach (var item in request.ShiftDetails)
                {
                    if (string.IsNullOrWhiteSpace(item.ShiftName) ||
                        !AppConstants.AllowedShiftNames.Any(s => string.Equals(s, item.ShiftName?.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        return new DoctorOverrideCreateResponseModel
                        {
                            Success = false,
                            Message = "Allowed values are Morning, Afternoon, Evening"
                        };
                    }
                }
            }

            var doctorExists = await _context.Doctors
                .Where(x => x.DoctorID == request.DoctorId)
                .Select(x => new { x.DoctorID, x.UserID })
                .FirstOrDefaultAsync(cancellationToken);

            if (doctorExists is not null)
            {
                DoctorOverrideCreateResponseModel responseModel = new();
                var startDateTime = request.StartDate.Date;
                var endDateTime = request.EndDate.Date;
                int updated = 0, added = 0;

                if (request.ShiftDetails != null)
                {
                    var currentDate = startDateTime;
                    while (currentDate <= endDateTime)
                    {
                        foreach (var item in request.ShiftDetails)
                        {
                            var shiftNameLower = item.ShiftName?.Trim().ToLower();
                            var existing = await _context.DoctorShiftOverrides
                                .FirstOrDefaultAsync(x => x.DoctorID == request.DoctorId &&
                                    x.StartDate == currentDate &&
                                    x.EndDate == currentDate &&
                                    x.ShiftName != null && x.ShiftName.Trim().ToLower() == shiftNameLower &&
                                    x.HospitalId == request.HospitalId, cancellationToken);

                            if (existing != null)
                            {
                                existing.StartTime = TimeSpan.Parse(item.StartTime ?? string.Empty);
                                existing.EndTime = TimeSpan.Parse(item.EndTime ?? string.Empty);
                                existing.SlotDurationInMinutes = item.SlotDurationInMinutes;
                                existing.RecurringDays = item.RecurringDays != null ? string.Join(",", item.RecurringDays) : null;
                                existing.OverrideDate = request.OverrideDate;
                                updated++;
                            }
                            else
                            {
                                var newOverride = new DoctorShiftOverride
                                {
                                    OverrideID = Guid.NewGuid(),
                                    DoctorID = request.DoctorId,
                                    HospitalId = request.HospitalId,
                                    ShiftName = item.ShiftName,
                                    StartTime = TimeSpan.Parse(item.StartTime ?? string.Empty),
                                    EndTime = TimeSpan.Parse(item.EndTime ?? string.Empty),
                                    SlotDurationInMinutes = item.SlotDurationInMinutes,
                                    RecurringDays = item.RecurringDays != null ? string.Join(",", item.RecurringDays) : null,
                                    StartDate = currentDate,
                                    EndDate = currentDate,
                                    CreatedAt = DateTime.UtcNow,
                                    OverrideDate = request.OverrideDate
                                };
                                _context.DoctorShiftOverrides.Add(newOverride);
                                added++;
                            }
                        }
                        currentDate = currentDate.AddDays(1);
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }

                responseModel.Success = true;
                responseModel.Message = $"Doctor Override(s) updated: {updated}, added: {added}";
                return responseModel;
            }
            else 
            { 
                return new DoctorOverrideCreateResponseModel
                {
                    Success = false,
                    Message = "Invalid Doctor Id"
                };
            }
        }
            
    }
}
