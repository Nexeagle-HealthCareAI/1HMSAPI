using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public (Nexeagle-facing) availability check — reuses DoctorSlotsHandler's exact resolution
    /// logic (time-off short-circuit, then override/template shift windows) but only reports
    /// whether the doctor is generally working that day, not a granular open-slot list: a public
    /// pre-appointment doesn't claim/lock a real time slot, so there's nothing to reconcile against
    /// booked appointments here. Additionally scopes the doctor lookup to the caller's own
    /// HospitalId (from the API key) so a public caller can't probe doctor ids from other hospitals.
    /// </summary>
    public class GetPublicDoctorAvailabilityHandler : IRequestHandler<GetPublicDoctorAvailabilityRequestModel, GetPublicDoctorAvailabilityResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicDoctorAvailabilityHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicDoctorAvailabilityResponseModel> Handle(GetPublicDoctorAvailabilityRequestModel request, CancellationToken cancellationToken)
        {
            var doctorExists = await (from d in _context.Doctors
                                       join u in _context.Users on d.UserID equals u.UserID
                                       where d.DoctorID == request.DoctorId
                                             && d.HospitalId == request.HospitalId
                                             && u.UserStatusId != (int)UserStatusEnum.Revoked
                                       select d.DoctorID).AnyAsync(cancellationToken);

            if (!doctorExists)
                return new GetPublicDoctorAvailabilityResponseModel { Success = false, Message = "Doctor not found." };

            var requestDate = request.Date.Date;

            var timeOff = await _context.DoctorTimeOffs
                .Where(to => to.DoctorID == request.DoctorId &&
                           to.HospitalId == request.HospitalId &&
                           requestDate >= to.FromDate.Date &&
                           requestDate <= to.ToDate.Date)
                .OrderByDescending(to => to.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (timeOff != null)
            {
                return new GetPublicDoctorAvailabilityResponseModel
                {
                    Success = true,
                    IsAvailable = false,
                    Reason = timeOff.Reason,
                };
            }

            var overrideShifts = await _context.DoctorShiftOverrides
                .Where(o => o.DoctorID == request.DoctorId &&
                          o.HospitalId == request.HospitalId &&
                          o.StartDate <= requestDate &&
                          (!o.EndDate.HasValue || o.EndDate >= requestDate))
                .OrderBy(o => o.StartTime)
                .ToListAsync(cancellationToken);

            List<PublicShiftInfo> shifts;
            if (overrideShifts.Count > 0)
            {
                shifts = overrideShifts
                    .Select(s => new PublicShiftInfo { Name = s.ShiftName, StartTime = s.StartTime, EndTime = s.EndTime })
                    .ToList();
            }
            else
            {
                shifts = await _context.DoctorShiftTemplates
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.StartTime)
                    .Select(t => new PublicShiftInfo { Name = t.ShiftName, StartTime = t.StartTime, EndTime = t.EndTime })
                    .ToListAsync(cancellationToken);
            }

            return new GetPublicDoctorAvailabilityResponseModel
            {
                Success = true,
                IsAvailable = shifts.Count > 0,
                Reason = shifts.Count > 0 ? null : "Doctor is not scheduled on this day.",
                Shifts = shifts,
            };
        }
    }
}
