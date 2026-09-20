using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Backs the appointment board's "a new online booking just came in" alert. Deliberately tiny --
    // one indexed lookup over public bookings created after a cursor -- because every open board
    // polls it every few seconds; the full board query (GetPatientAppointmentDetailsHandler) is far
    // too heavy for that cadence, and being date-scoped it can't see a booking made for another day.
    public class GetRecentOnlineBookingsHandler : IRequestHandler<GetRecentOnlineBookingsRequestModel, GetRecentOnlineBookingsResponseModel>
    {
        private const int MaxItems = 20;
        // A tab that was backgrounded (polling paused) and comes back shouldn't replay a whole
        // day of bookings -- anything older than this is what the board's own list is for.
        private static readonly TimeSpan MaxLookback = TimeSpan.FromHours(12);
        // CreatedAt is stamped when the booking handler runs but only becomes visible at commit, so
        // a row can appear slightly "in the past" relative to a cursor taken between the two. Re-reading
        // a small window each poll closes that gap; the client de-duplicates by appointment id.
        private static readonly TimeSpan CursorOverlap = TimeSpan.FromSeconds(15);

        private readonly AppDbContext _context;

        public GetRecentOnlineBookingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRecentOnlineBookingsResponseModel> Handle(GetRecentOnlineBookingsRequestModel request, CancellationToken cancellationToken)
        {
            var serverTime = DateTime.UtcNow;
            var response = new GetRecentOnlineBookingsResponseModel { ServerTime = serverTime };

            // First call: just hand back the cursor.
            if (!request.Since.HasValue) return response;

            var since = request.Since.Value.ToUniversalTime() - CursorOverlap;
            var earliest = serverTime - MaxLookback;
            if (since < earliest) since = earliest;

            var rows = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.HospitalId == request.HospitalId
                    && a.BookingSource == AppConstants.BookingSource_NexeaglePublic
                    && a.CreatedAt > since)
                .OrderByDescending(a => a.CreatedAt)
                .Take(MaxItems)
                .Select(a => new { a.ApptId, a.PatientId, a.DoctorId, a.ApptDate, a.StartAt, a.CreatedAt, a.CurrentStatusCode })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0) return response;

            var patientIds = rows.Select(r => r.PatientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var patientNames = await _context.PatientRegistrations
                .AsNoTracking()
                .Where(p => p.PatientId != null && patientIds.Contains(p.PatientId!))
                .Select(p => new { p.PatientId, p.FullName })
                .ToDictionaryAsync(p => p.PatientId!, p => p.FullName, cancellationToken);

            var doctorIds = rows.Select(r => r.DoctorId).Distinct().ToList();
            var doctorNames = await (from d in _context.Doctors.AsNoTracking()
                                     join u in _context.UserProfiles.AsNoTracking() on d.UserID equals u.UserID
                                     where doctorIds.Contains(d.DoctorID)
                                     select new { d.DoctorID, u.FullName })
                .ToDictionaryAsync(x => x.DoctorID, x => x.FullName, cancellationToken);

            response.Items = rows.Select(r => new RecentOnlineBookingItem
            {
                AppointmentId = r.ApptId,
                PatientName = r.PatientId != null && patientNames.TryGetValue(r.PatientId, out var patientName) ? patientName : null,
                DoctorName = doctorNames.TryGetValue(r.DoctorId, out var doctorName) ? doctorName : null,
                ApptDate = r.ApptDate,
                StartAt = r.StartAt,
                CreatedAt = r.CreatedAt,
                Status = r.CurrentStatusCode,
            }).ToList();

            return response;
        }
    }
}
