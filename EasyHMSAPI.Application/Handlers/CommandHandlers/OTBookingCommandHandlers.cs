using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Theatre master + booking. Overlap conflict-check is application-level (query existing active
    /// bookings for the theatre, reject if the requested window intersects) — a best-effort guard
    /// like BedAssignment's DbUpdateException backstop, not a DB-enforced hard guarantee (SQL
    /// Server has no time-range exclusion constraint).
    /// </summary>
    public class OTBookingCommandHandlers :
        IRequestHandler<CreateOperationTheatreRequestModel, CreateOperationTheatreResponseModel>,
        IRequestHandler<CreateOTBookingRequestModel, CreateOTBookingResponseModel>,
        IRequestHandler<RescheduleOTBookingRequestModel, RescheduleOTBookingResponseModel>,
        IRequestHandler<CancelOTBookingRequestModel, CancelOTBookingResponseModel>
    {
        private readonly AppDbContext _context;

        public OTBookingCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateOperationTheatreResponseModel> Handle(CreateOperationTheatreRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.TheatreCode) || string.IsNullOrWhiteSpace(request.TheatreName))
                    return new CreateOperationTheatreResponseModel { Success = false, Message = "HospitalId, TheatreCode, and TheatreName are required." };

                var exists = await _context.OperationTheatre.AnyAsync(
                    t => t.HospitalId == request.HospitalId && t.TheatreCode == request.TheatreCode.Trim(), cancellationToken);
                if (exists)
                    return new CreateOperationTheatreResponseModel { Success = false, Message = "A theatre with this code already exists." };

                var now = DateTime.UtcNow;
                var theatre = new OperationTheatre
                {
                    TheatreId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    TheatreCode = request.TheatreCode.Trim(),
                    TheatreName = request.TheatreName.Trim(),
                    Status = IpdConstants.TheatreStatus.Available,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.OperationTheatre.Add(theatre);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateOperationTheatreResponseModel { Success = true, Message = "Theatre created.", TheatreId = theatre.TheatreId };
            }
            catch (Exception)
            {
                return new CreateOperationTheatreResponseModel { Success = false, Message = "Error creating theatre." };
            }
        }

        public async Task<CreateOTBookingResponseModel> Handle(CreateOTBookingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty || request.TheatreId == Guid.Empty)
                    return new CreateOTBookingResponseModel { Success = false, Message = "HospitalId, SurgeryCaseId, and TheatreId are required." };
                if (request.ScheduledEnd <= request.ScheduledStart)
                    return new CreateOTBookingResponseModel { Success = false, Message = "ScheduledEnd must be after ScheduledStart." };

                var surgeryCase = await _context.SurgeryCase
                    .FirstOrDefaultAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
                if (surgeryCase == null)
                    return new CreateOTBookingResponseModel { Success = false, Message = "Surgery case not found." };

                var alreadyBooked = await _context.OTBooking.AnyAsync(
                    b => b.SurgeryCaseId == request.SurgeryCaseId && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode), cancellationToken);
                if (alreadyBooked)
                    return new CreateOTBookingResponseModel { Success = false, Message = "This case already has an active booking — reschedule it instead." };

                var conflict = await HasTheatreConflictAsync(request.HospitalId, request.TheatreId, request.ScheduledStart, request.ScheduledEnd, excludeBookingId: null, cancellationToken);
                if (conflict)
                    return new CreateOTBookingResponseModel { Success = false, Message = "This theatre is already booked for an overlapping time." };

                var now = DateTime.UtcNow;
                var booking = new OTBooking
                {
                    OTBookingId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    SurgeryCaseId = request.SurgeryCaseId,
                    TheatreId = request.TheatreId,
                    ScheduledStart = request.ScheduledStart,
                    ScheduledEnd = request.ScheduledEnd,
                    StatusCode = IpdConstants.OTBookingStatus.Scheduled,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.OTBooking.Add(booking);

                if (surgeryCase.StatusCode == IpdConstants.SurgeryStatus.Requested)
                {
                    surgeryCase.StatusCode = IpdConstants.SurgeryStatus.Scheduled;
                    surgeryCase.UpdatedAt = now;
                    surgeryCase.UpdatedBy = request.LoggedInUserName;
                    _context.SurgeryStatusHistory.Add(new SurgeryStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        SurgeryCaseId = surgeryCase.SurgeryCaseId,
                        FromStatus = IpdConstants.SurgeryStatus.Requested,
                        ToStatus = IpdConstants.SurgeryStatus.Scheduled,
                        ChangedAt = now,
                        ChangedBy = request.LoggedInUserName,
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateOTBookingResponseModel { Success = true, Message = "Booking created.", OTBookingId = booking.OTBookingId };
            }
            catch (Exception)
            {
                return new CreateOTBookingResponseModel { Success = false, Message = "Error creating booking." };
            }
        }

        public async Task<RescheduleOTBookingResponseModel> Handle(RescheduleOTBookingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.OTBookingId == Guid.Empty || request.TheatreId == Guid.Empty)
                    return new RescheduleOTBookingResponseModel { Success = false, Message = "HospitalId, OTBookingId, and TheatreId are required." };
                if (request.ScheduledEnd <= request.ScheduledStart)
                    return new RescheduleOTBookingResponseModel { Success = false, Message = "ScheduledEnd must be after ScheduledStart." };

                var booking = await _context.OTBooking
                    .FirstOrDefaultAsync(b => b.OTBookingId == request.OTBookingId && b.HospitalId == request.HospitalId, cancellationToken);
                if (booking == null)
                    return new RescheduleOTBookingResponseModel { Success = false, Message = "Booking not found." };
                if (!IpdConstants.OTBookingStatus.Active.Contains(booking.StatusCode))
                    return new RescheduleOTBookingResponseModel { Success = false, Message = "This booking is no longer active." };

                var conflict = await HasTheatreConflictAsync(request.HospitalId, request.TheatreId, request.ScheduledStart, request.ScheduledEnd, excludeBookingId: booking.OTBookingId, cancellationToken);
                if (conflict)
                    return new RescheduleOTBookingResponseModel { Success = false, Message = "This theatre is already booked for an overlapping time." };

                booking.TheatreId = request.TheatreId;
                booking.ScheduledStart = request.ScheduledStart;
                booking.ScheduledEnd = request.ScheduledEnd;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new RescheduleOTBookingResponseModel { Success = true, Message = "Booking rescheduled." };
            }
            catch (Exception)
            {
                return new RescheduleOTBookingResponseModel { Success = false, Message = "Error rescheduling booking." };
            }
        }

        public async Task<CancelOTBookingResponseModel> Handle(CancelOTBookingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.OTBookingId == Guid.Empty)
                    return new CancelOTBookingResponseModel { Success = false, Message = "HospitalId and OTBookingId are required." };

                var booking = await _context.OTBooking
                    .FirstOrDefaultAsync(b => b.OTBookingId == request.OTBookingId && b.HospitalId == request.HospitalId, cancellationToken);
                if (booking == null)
                    return new CancelOTBookingResponseModel { Success = false, Message = "Booking not found." };
                if (!IpdConstants.OTBookingStatus.Active.Contains(booking.StatusCode))
                    return new CancelOTBookingResponseModel { Success = false, Message = "This booking is no longer active." };

                booking.StatusCode = IpdConstants.OTBookingStatus.Cancelled;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new CancelOTBookingResponseModel { Success = true, Message = "Booking cancelled." };
            }
            catch (Exception)
            {
                return new CancelOTBookingResponseModel { Success = false, Message = "Error cancelling booking." };
            }
        }

        private async Task<bool> HasTheatreConflictAsync(Guid hospitalId, Guid theatreId, DateTime start, DateTime end, Guid? excludeBookingId, CancellationToken cancellationToken)
        {
            var query = _context.OTBooking.Where(b =>
                b.HospitalId == hospitalId
                && b.TheatreId == theatreId
                && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode)
                && b.ScheduledStart < end
                && b.ScheduledEnd > start);

            if (excludeBookingId.HasValue)
                query = query.Where(b => b.OTBookingId != excludeBookingId.Value);

            return await query.AnyAsync(cancellationToken);
        }
    }
}
