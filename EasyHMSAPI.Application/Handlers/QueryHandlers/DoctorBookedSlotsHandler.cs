using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorBookedSlotsHandler : IRequestHandler<DoctorBookedSlotsRequestModel, DoctorBookedSlotsResponseModel>
    {
        // Short on purpose: unlike the doctor-directory/availability caches, staleness here
        // directly increases how often a receptionist picks a slot that then loses a real
        // conflict check at submit time. The backend conflict check (ConfirmPreAppointmentHandler/
        // RegisterAppointmentHandler) is still the actual safety net either way, but a long TTL
        // would make retries needlessly common.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public DoctorBookedSlotsHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<DoctorBookedSlotsResponseModel> Handle(DoctorBookedSlotsRequestModel request, CancellationToken cancellationToken)
        {
            var requestDate = request.Date.Date;
            // ExcludeAppointmentId (the pre-appointment-confirm case) is caller-specific and rarer
            // — cache only the shared, high-volume "plain availability check" shape, and always
            // hit the DB fresh for an exclude-scoped request rather than trying to key the cache
            // by that too.
            var cacheKey = request.ExcludeAppointmentId == null
                ? PublicDirectoryCacheKeys.BookedSlots(request.HospitalId, request.DoctorId, requestDate)
                : null;

            if (cacheKey != null && _cache.TryGetValue(cacheKey, out DoctorBookedSlotsResponseModel? cached) && cached != null)
            {
                return cached;
            }

            // Fully read-only, one of the hottest paths in the app — hit on every availability
            // check, every booking, and every pre-appointment confirm. See GetPublicDoctorsHandler.
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var bookedSlots = await (from a in _context.Appointments
                                     join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                     join u in _context.Users on d.UserID equals u.UserID
                                     where a.DoctorId == request.DoctorId && a.HospitalId == request.HospitalId && a.ApptDate.Date == requestDate && u.UserStatusId != (int)UserStatusEnum.Revoked && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                                           && (request.ExcludeAppointmentId == null || a.ApptId != request.ExcludeAppointmentId.Value)
                                     select a.StartAt.TimeOfDay)
                                     .ToListAsync(cancellationToken);

            var response = new DoctorBookedSlotsResponseModel
            {
                DoctorId = request.DoctorId,
                Date = requestDate,
                BookedSlots = bookedSlots
            };

            if (cacheKey != null)
            {
                _cache.Set(cacheKey, response, CacheTtl);
            }

            return response;
        }
    }
}
