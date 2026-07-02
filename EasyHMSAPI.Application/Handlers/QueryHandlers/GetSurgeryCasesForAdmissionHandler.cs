using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetSurgeryCasesForAdmissionHandler : IRequestHandler<GetSurgeryCasesForAdmissionRequestModel, GetSurgeryCasesForAdmissionResponseModel>
    {
        private readonly AppDbContext _context;

        public GetSurgeryCasesForAdmissionHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetSurgeryCasesForAdmissionResponseModel> Handle(GetSurgeryCasesForAdmissionRequestModel request, CancellationToken cancellationToken)
        {
            var cases = await _context.SurgeryCase
                .Where(s => s.HospitalId == request.HospitalId && s.AdmissionId == request.AdmissionId)
                .OrderByDescending(s => s.RequestedAt)
                .ToListAsync(cancellationToken);

            var caseIds = cases.Select(c => c.SurgeryCaseId).ToList();
            var bookings = await _context.OTBooking
                .Where(b => caseIds.Contains(b.SurgeryCaseId) && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode))
                .ToListAsync(cancellationToken);
            var bookingByCase = bookings.ToDictionary(b => b.SurgeryCaseId);

            var theatreIds = bookings.Select(b => b.TheatreId).Distinct().ToList();
            var theatresById = await _context.OperationTheatre
                .Where(t => theatreIds.Contains(t.TheatreId))
                .ToDictionaryAsync(t => t.TheatreId, cancellationToken);

            var items = cases.Select(c =>
            {
                bookingByCase.TryGetValue(c.SurgeryCaseId, out var booking);
                string? theatreName = null;
                if (booking != null && theatresById.TryGetValue(booking.TheatreId, out var theatre))
                    theatreName = theatre.TheatreName;

                return new SurgeryCaseSummaryDataModel
                {
                    SurgeryCaseId = c.SurgeryCaseId,
                    ProcedureName = c.ProcedureName,
                    SurgeryType = c.SurgeryType,
                    Urgency = c.Urgency,
                    StatusCode = c.StatusCode,
                    RequestedAt = c.RequestedAt,
                    SurgeonName = c.SurgeonName,
                    AnaesthetistName = c.AnaesthetistName,
                    ScheduledStart = booking?.ScheduledStart,
                    ScheduledEnd = booking?.ScheduledEnd,
                    TheatreName = theatreName,
                };
            }).ToList();

            return new GetSurgeryCasesForAdmissionResponseModel { Cases = items };
        }
    }
}
