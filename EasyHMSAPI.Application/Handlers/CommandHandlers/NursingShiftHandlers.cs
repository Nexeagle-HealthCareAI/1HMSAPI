using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class NursingShiftDto
    {
        public string ShiftCode { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class GetNursingShiftsRequestModel : IRequest<GetNursingShiftsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    public class GetNursingShiftsResponseModel
    {
        public bool Success { get; set; } = true;
        /// <summary>Empty when the hospital has never saved shifts (clients then use their defaults).</summary>
        public List<NursingShiftDto> Shifts { get; set; } = new();
    }

    public class SaveNursingShiftsRequestModel : IRequest<SaveNursingShiftsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public List<NursingShiftDto> Shifts { get; set; } = new();
    }

    public class SaveNursingShiftsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<NursingShiftDto> Shifts { get; set; } = new();
    }

    public class GetNursingShiftsHandler : IRequestHandler<GetNursingShiftsRequestModel, GetNursingShiftsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetNursingShiftsHandler(AppDbContext context) => _context = context;

        public async Task<GetNursingShiftsResponseModel> Handle(GetNursingShiftsRequestModel request, CancellationToken cancellationToken)
        {
            var rows = await _context.Set<NursingShift>().AsNoTracking()
                .Where(s => s.HospitalId == request.HospitalId)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.ShiftCode)
                .ToListAsync(cancellationToken);
            return new GetNursingShiftsResponseModel { Shifts = rows.Select(ToDto).ToList() };
        }

        internal static NursingShiftDto ToDto(NursingShift s) => new()
        {
            ShiftCode = s.ShiftCode, Label = s.Label, StartTime = s.StartTime, EndTime = s.EndTime, IsActive = s.IsActive, SortOrder = s.SortOrder,
        };
    }

    /// <summary>
    /// Replaces the hospital's whole shift list (matching how the UI edits it). Shifts that are no
    /// longer present are removed; codes are upper-cased and must be unique.
    /// </summary>
    public class SaveNursingShiftsHandler : IRequestHandler<SaveNursingShiftsRequestModel, SaveNursingShiftsResponseModel>
    {
        private static readonly Regex CodePattern = new(@"^[A-Z0-9_\-]{1,30}$", RegexOptions.Compiled);
        private static readonly Regex TimePattern = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

        private readonly AppDbContext _context;
        public SaveNursingShiftsHandler(AppDbContext context) => _context = context;

        public async Task<SaveNursingShiftsResponseModel> Handle(SaveNursingShiftsRequestModel request, CancellationToken cancellationToken)
        {
            var incoming = new List<NursingShiftDto>();
            foreach (var s in request.Shifts ?? new List<NursingShiftDto>())
            {
                var code = (s.ShiftCode ?? string.Empty).Trim().ToUpperInvariant();
                var label = (s.Label ?? string.Empty).Trim();
                if (!CodePattern.IsMatch(code))
                    return Fail($"Invalid shift code '{s.ShiftCode}'. Use letters, digits, '-' or '_' (max 30).");
                if (label.Length == 0 || label.Length > 60)
                    return Fail($"Shift '{code}' needs a name (max 60 characters).");
                if (!string.IsNullOrWhiteSpace(s.StartTime) && !TimePattern.IsMatch(s.StartTime))
                    return Fail($"Shift '{code}' has an invalid start time (use HH:mm).");
                if (!string.IsNullOrWhiteSpace(s.EndTime) && !TimePattern.IsMatch(s.EndTime))
                    return Fail($"Shift '{code}' has an invalid end time (use HH:mm).");
                if (incoming.Any(x => x.ShiftCode == code))
                    return Fail($"Shift code '{code}' is used twice.");

                incoming.Add(new NursingShiftDto
                {
                    ShiftCode = code,
                    Label = label,
                    StartTime = string.IsNullOrWhiteSpace(s.StartTime) ? null : s.StartTime,
                    EndTime = string.IsNullOrWhiteSpace(s.EndTime) ? null : s.EndTime,
                    IsActive = s.IsActive,
                    SortOrder = s.SortOrder,
                });
            }

            var existing = await _context.Set<NursingShift>().Where(s => s.HospitalId == request.HospitalId).ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var userId = request.UserId == Guid.Empty ? (Guid?)null : request.UserId;

            foreach (var gone in existing.Where(e => incoming.All(i => i.ShiftCode != e.ShiftCode)))
                _context.Set<NursingShift>().Remove(gone);

            foreach (var dto in incoming)
            {
                var row = existing.FirstOrDefault(e => e.ShiftCode == dto.ShiftCode);
                if (row == null)
                {
                    row = new NursingShift { HospitalId = request.HospitalId, ShiftCode = dto.ShiftCode };
                    _context.Set<NursingShift>().Add(row);
                }
                row.Label = dto.Label;
                row.StartTime = dto.StartTime;
                row.EndTime = dto.EndTime;
                row.IsActive = dto.IsActive;
                row.SortOrder = dto.SortOrder;
                row.UpdatedAt = now;
                row.UpdatedByUserId = userId;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return new SaveNursingShiftsResponseModel { Success = true, Shifts = incoming.OrderBy(s => s.SortOrder).ToList() };
        }

        private static SaveNursingShiftsResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
