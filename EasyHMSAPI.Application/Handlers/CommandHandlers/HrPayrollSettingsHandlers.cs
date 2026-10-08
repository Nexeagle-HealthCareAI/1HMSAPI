using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // ───────── Payroll calendar policy: weekly-off days + whether weekly offs / holidays are payable ─────────

    public class PayrollSettingsDto
    {
        /// <summary>Weekday codes, e.g. ["SUN"] or ["SAT","SUN"]. Empty = no weekly off.</summary>
        public List<string> WeeklyOffDays { get; set; } = new() { "SUN" };
        public bool WeeklyOffPayable { get; set; } = true;
        public bool HolidayPayable { get; set; } = true;
    }

    public class HolidayDto
    {
        public Guid HrHolidayId { get; set; }
        public DateOnly HolidayDate { get; set; }
        public string Name { get; set; } = null!;
    }

    public class GetPayrollSettingsRequestModel : IRequest<GetPayrollSettingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        /// <summary>Optional year filter for the holiday list; defaults to the current year.</summary>
        public int? Year { get; set; }
    }

    public class GetPayrollSettingsResponseModel
    {
        public bool Success { get; set; } = true;
        public PayrollSettingsDto Settings { get; set; } = new();
        public List<HolidayDto> Holidays { get; set; } = new();
    }

    public class SavePayrollSettingsRequestModel : IRequest<SimpleHrResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public PayrollSettingsDto Settings { get; set; } = new();
    }

    public class AddHolidayRequestModel : IRequest<SimpleHrResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public DateOnly HolidayDate { get; set; }
        public string Name { get; set; } = null!;
    }

    public class DeleteHolidayRequestModel : IRequest<SimpleHrResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid HrHolidayId { get; set; }
    }

    public class SimpleHrResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class GetPayrollSettingsHandler : IRequestHandler<GetPayrollSettingsRequestModel, GetPayrollSettingsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetPayrollSettingsHandler(AppDbContext context) => _context = context;

        public async Task<GetPayrollSettingsResponseModel> Handle(GetPayrollSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var year = request.Year ?? DateTime.UtcNow.Year;
            var settings = await _context.Set<HrPayrollSettings>().AsNoTracking()
                .FirstOrDefaultAsync(s => s.HospitalId == request.HospitalId, cancellationToken);
            var from = new DateOnly(year, 1, 1);
            var to = new DateOnly(year, 12, 31);
            var holidays = await _context.Set<HrHoliday>().AsNoTracking()
                .Where(h => h.HospitalId == request.HospitalId && h.HolidayDate >= from && h.HolidayDate <= to)
                .OrderBy(h => h.HolidayDate)
                .Select(h => new HolidayDto { HrHolidayId = h.HrHolidayId, HolidayDate = h.HolidayDate, Name = h.Name })
                .ToListAsync(cancellationToken);

            var dto = settings == null
                ? new PayrollSettingsDto()
                : new PayrollSettingsDto
                {
                    WeeklyOffDays = PayrollCalendarPolicy.FormatWeeklyOffDays(PayrollCalendarPolicy.ParseWeeklyOffDays(settings.WeeklyOffDays))
                        .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    WeeklyOffPayable = settings.WeeklyOffPayable,
                    HolidayPayable = settings.HolidayPayable,
                };
            return new GetPayrollSettingsResponseModel { Settings = dto, Holidays = holidays };
        }
    }

    public class SavePayrollSettingsHandler : IRequestHandler<SavePayrollSettingsRequestModel, SimpleHrResponseModel>
    {
        private readonly AppDbContext _context;
        public SavePayrollSettingsHandler(AppDbContext context) => _context = context;

        public async Task<SimpleHrResponseModel> Handle(SavePayrollSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var days = PayrollCalendarPolicy.ParseWeeklyOffDays(string.Join(",", request.Settings.WeeklyOffDays ?? new List<string>()));
            var csv = PayrollCalendarPolicy.FormatWeeklyOffDays(days);

            var row = await _context.Set<HrPayrollSettings>().FirstOrDefaultAsync(s => s.HospitalId == request.HospitalId, cancellationToken);
            if (row == null)
            {
                row = new HrPayrollSettings { HospitalId = request.HospitalId };
                _context.Set<HrPayrollSettings>().Add(row);
            }
            row.WeeklyOffDays = csv;
            row.WeeklyOffPayable = request.Settings.WeeklyOffPayable;
            row.HolidayPayable = request.Settings.HolidayPayable;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedByUserId = request.UserId == Guid.Empty ? null : request.UserId;
            await _context.SaveChangesAsync(cancellationToken);
            return new SimpleHrResponseModel { Success = true, Message = "Payroll settings saved. They apply to the next payroll run." };
        }
    }

    public class AddHolidayHandler : IRequestHandler<AddHolidayRequestModel, SimpleHrResponseModel>
    {
        private readonly AppDbContext _context;
        public AddHolidayHandler(AppDbContext context) => _context = context;

        public async Task<SimpleHrResponseModel> Handle(AddHolidayRequestModel request, CancellationToken cancellationToken)
        {
            var name = (request.Name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 120)
                return new SimpleHrResponseModel { Success = false, Message = "Holiday name is required (max 120 characters)." };

            var exists = await _context.Set<HrHoliday>()
                .AnyAsync(h => h.HospitalId == request.HospitalId && h.HolidayDate == request.HolidayDate, cancellationToken);
            if (exists)
                return new SimpleHrResponseModel { Success = false, Message = "A holiday is already declared on that date." };

            _context.Set<HrHoliday>().Add(new HrHoliday
            {
                HospitalId = request.HospitalId,
                HolidayDate = request.HolidayDate,
                Name = name,
                CreatedByUserId = request.UserId == Guid.Empty ? null : request.UserId,
            });
            await _context.SaveChangesAsync(cancellationToken);
            return new SimpleHrResponseModel { Success = true, Message = "Holiday added." };
        }
    }

    public class DeleteHolidayHandler : IRequestHandler<DeleteHolidayRequestModel, SimpleHrResponseModel>
    {
        private readonly AppDbContext _context;
        public DeleteHolidayHandler(AppDbContext context) => _context = context;

        public async Task<SimpleHrResponseModel> Handle(DeleteHolidayRequestModel request, CancellationToken cancellationToken)
        {
            // Hospital-scoped: an ID alone never reaches another hospital's row.
            var row = await _context.Set<HrHoliday>()
                .FirstOrDefaultAsync(h => h.HrHolidayId == request.HrHolidayId && h.HospitalId == request.HospitalId, cancellationToken);
            if (row == null) return new SimpleHrResponseModel { Success = false, Message = "Holiday not found." };
            _context.Set<HrHoliday>().Remove(row);
            await _context.SaveChangesAsync(cancellationToken);
            return new SimpleHrResponseModel { Success = true, Message = "Holiday removed." };
        }
    }
}
