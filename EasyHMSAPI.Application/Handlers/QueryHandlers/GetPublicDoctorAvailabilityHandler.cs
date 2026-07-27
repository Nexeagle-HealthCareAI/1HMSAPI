using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public (Nexeagle-facing) availability check — reuses DoctorSlotsHandler's exact resolution
    /// logic (time-off short-circuit, then override/template shift windows) but only reports
    /// whether the doctor is generally working that day, not a granular open-slot list: a public
    /// pre-appointment doesn't claim/lock a real time slot, so there's nothing to reconcile against
    /// booked appointments here. Resolves HospitalId from the doctor itself via
    /// PublicDirectoryHelpers (never a client-supplied value) and gates on both Hospital.IsPubliclyListed
    /// and Doctor.IsPubliclyListed, so a public caller can't reach a doctor/hospital pair that hasn't
    /// opted into the directory.
    /// </summary>
    public class GetPublicDoctorAvailabilityHandler : IRequestHandler<GetPublicDoctorAvailabilityRequestModel, GetPublicDoctorAvailabilityResponseModel>
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public GetPublicDoctorAvailabilityHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<GetPublicDoctorAvailabilityResponseModel> Handle(GetPublicDoctorAvailabilityRequestModel request, CancellationToken cancellationToken)
        {
            // Shift schedules and time-off change rarely compared to how often many concurrent
            // visitors check the same popular doctor+date — a doctor's resolved hospital doesn't
            // change within a 30s window either, so keying purely on (doctorId, date) lets a cache
            // hit skip the hospital-resolution lookup too, not just the shift/time-off queries.
            var cacheKey = PublicDirectoryCacheKeys.DoctorAvailability(request.DoctorId, request.Date.Date);
            if (_cache.TryGetValue(cacheKey, out GetPublicDoctorAvailabilityResponseModel? cached) && cached != null)
            {
                return cached;
            }

            // Fully read-only, hit on every doctor+date the public site checks — skip EF Core's
            // change-tracking bookkeeping for the whole request (see GetPublicDoctorsHandler).
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var doctorHospitalId = await PublicDirectoryHelpers.ResolvePubliclyListedHospitalIdAsync(_context, request.DoctorId, cancellationToken);

            if (doctorHospitalId == null)
                return new GetPublicDoctorAvailabilityResponseModel { Success = false, Message = "Doctor not found." };

            var hospitalId = doctorHospitalId.Value;
            var requestDate = request.Date.Date;

            var timeOff = await _context.DoctorTimeOffs
                .Where(to => to.DoctorID == request.DoctorId &&
                           to.HospitalId == hospitalId &&
                           requestDate >= to.FromDate.Date &&
                           requestDate <= to.ToDate.Date)
                .OrderByDescending(to => to.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (timeOff != null)
            {
                var timeOffResponse = new GetPublicDoctorAvailabilityResponseModel
                {
                    Success = true,
                    IsAvailable = false,
                    Reason = timeOff.Reason,
                };
                _cache.Set(cacheKey, timeOffResponse, CacheTtl);
                return timeOffResponse;
            }

            var overrideShifts = await _context.DoctorShiftOverrides
                .Where(o => o.DoctorID == request.DoctorId &&
                          o.HospitalId == hospitalId &&
                          o.StartDate <= requestDate &&
                          (!o.EndDate.HasValue || o.EndDate >= requestDate))
                .ToListAsync(cancellationToken);

            // Templates are global (not per-doctor) — only worth fetching when there's no override,
            // since an override for the date always wins.
            var activeTemplates = overrideShifts.Count > 0
                ? new List<DoctorShiftTemplate>()
                : await _context.DoctorShiftTemplates.Where(t => t.IsActive).ToListAsync(cancellationToken);

            var shifts = DoctorAvailabilityResolver.ResolveShifts(overrideShifts, activeTemplates);

            var response = new GetPublicDoctorAvailabilityResponseModel
            {
                Success = true,
                IsAvailable = shifts.Count > 0,
                Reason = shifts.Count > 0 ? null : "Doctor is not scheduled on this day.",
                Shifts = shifts,
            };
            _cache.Set(cacheKey, response, CacheTtl);
            return response;
        }
    }
}
