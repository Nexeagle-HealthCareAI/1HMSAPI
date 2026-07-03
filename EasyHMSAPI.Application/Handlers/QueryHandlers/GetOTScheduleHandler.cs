using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetOTScheduleHandler : IRequestHandler<GetOTScheduleRequestModel, GetOTScheduleResponseModel>
    {
        private readonly AppDbContext _context;

        public GetOTScheduleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetOTScheduleResponseModel> Handle(GetOTScheduleRequestModel request, CancellationToken cancellationToken)
        {
            var bookings = await _context.OTBooking
                .Where(b => b.HospitalId == request.HospitalId
                    && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode)
                    && b.ScheduledStart < request.ToDate
                    && b.ScheduledEnd > request.FromDate)
                .OrderBy(b => b.ScheduledStart)
                .ToListAsync(cancellationToken);

            var theatreIds = bookings.Select(b => b.TheatreId).Distinct().ToList();
            var theatresById = await _context.OperationTheatre
                .Where(t => theatreIds.Contains(t.TheatreId))
                .ToDictionaryAsync(t => t.TheatreId, cancellationToken);

            var caseIds = bookings.Select(b => b.SurgeryCaseId).Distinct().ToList();
            var casesById = await _context.SurgeryCase
                .Where(s => caseIds.Contains(s.SurgeryCaseId))
                .ToDictionaryAsync(s => s.SurgeryCaseId, cancellationToken);

            var patientIds = casesById.Values.Select(s => s.PatientId).Where(p => p != null).Distinct().ToList();
            var patientsById = await _context.PatientRegistrations
                .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

            var items = bookings.Select(b =>
            {
                theatresById.TryGetValue(b.TheatreId, out var theatre);
                casesById.TryGetValue(b.SurgeryCaseId, out var surgeryCase);
                string? patientName = null;
                if (surgeryCase?.PatientId != null && patientsById.TryGetValue(surgeryCase.PatientId, out var patient))
                    patientName = patient.FullName;

                return new OTBookingDataModel
                {
                    OTBookingId = b.OTBookingId,
                    SurgeryCaseId = b.SurgeryCaseId,
                    TheatreId = b.TheatreId,
                    TheatreCode = theatre?.TheatreCode,
                    TheatreName = theatre?.TheatreName,
                    ProcedureName = surgeryCase?.ProcedureName,
                    PatientName = patientName,
                    ScheduledStart = b.ScheduledStart,
                    ScheduledEnd = b.ScheduledEnd,
                    StatusCode = b.StatusCode,
                };
            }).ToList();

            return new GetOTScheduleResponseModel { Bookings = items };
        }
    }
}
