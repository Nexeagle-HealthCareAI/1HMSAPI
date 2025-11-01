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
                    if (string.IsNullOrWhiteSpace(item.ShiftName) || !AppConstants.AllowedShiftNames.Contains(item.ShiftName.Trim().ToLower()))
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
                var startDateTime = request.StartDate;
                var endDateTime = request.EndDate;
                int updated = 0, added = 0;

                var overrideRecords = await _context.DoctorShiftOverrides
                    .Where(x => x.DoctorID == request.DoctorId &&
                        x.EndDate == endDateTime.Date && x.StartDate == startDateTime.Date)
                    .ToListAsync(cancellationToken);
                
                if(overrideRecords.Count > 0)
                {
                    if (request.ShiftDetails != null)
                    {
                        foreach (var item in request.ShiftDetails)
                        {
                            var existing = overrideRecords.FirstOrDefault(x => x.ShiftName?.Trim().ToLower() == item.ShiftName?.Trim().ToLower());
                            if (existing != null)
                            {
                                existing.StartTime = TimeSpan.Parse(item.StartTime ?? string.Empty);
                                existing.EndTime = TimeSpan.Parse(item.EndTime ?? string.Empty);
                                existing.SlotDurationInMinutes = item.SlotDurationInMinutes;
                                existing.RecurringDays = item.RecurringDays != null ? string.Join(",", item.RecurringDays) : null;
                                existing.StartDate = startDateTime;
                                existing.EndDate = endDateTime;
                                existing.OverrideDate = request.OverrideDate;
                                updated++;
                            }
                            else
                            {
                                var newOverride = new DoctorShiftOverride
                                {
                                    OverrideID = Guid.NewGuid(),
                                    DoctorID = request.DoctorId,
                                    ShiftName = item.ShiftName,
                                    StartTime = TimeSpan.Parse(item.StartTime ?? string.Empty),
                                    EndTime = TimeSpan.Parse(item.EndTime ?? string.Empty),
                                    SlotDurationInMinutes = item.SlotDurationInMinutes,
                                    RecurringDays = item.RecurringDays != null ? string.Join(",", item.RecurringDays) : null,
                                    StartDate = startDateTime,
                                    EndDate = endDateTime,
                                    CreatedAt = DateTime.UtcNow,
                                    OverrideDate = request.OverrideDate
                                };
                                _context.DoctorShiftOverrides.Add(newOverride);
                                added++;
                            }
                        }
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    responseModel.Success = true;
                    responseModel.Message = $"Doctor Override(s) updated: {updated}, added: {added}";
                }
                else
                {
                    if (request.ShiftDetails != null)
                    {
                        foreach (var item in request.ShiftDetails)
                        {
                            var newOverride = new DoctorShiftOverride
                            {
                                OverrideID = Guid.NewGuid(),
                                DoctorID = request.DoctorId,
                                ShiftName = item.ShiftName,
                                StartTime = TimeSpan.Parse(item.StartTime ?? string.Empty),
                                EndTime = TimeSpan.Parse(item.EndTime ?? string.Empty),
                                SlotDurationInMinutes = item.SlotDurationInMinutes,
                                RecurringDays = item.RecurringDays != null ? string.Join(",", item.RecurringDays) : null,
                                StartDate = request.StartDate,
                                EndDate = request.EndDate,
                                CreatedAt = DateTime.UtcNow,
                                OverrideDate = request.OverrideDate
                            };
                            _context.DoctorShiftOverrides.Add(newOverride);
                            added++;
                        }
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    responseModel.Success = true;
                    responseModel.Message = $"Doctor Override(s) added: {added}";
                }

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
