using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Kanban plan board data source — case-centric (not booking-centric like GetOTScheduleHandler)
    // so a REQUESTED/SCHEDULED case with no theatre booked yet still shows up in its column.
    public class GetOtBoardHandler : IRequestHandler<GetOtBoardRequestModel, GetOtBoardResponseModel>
    {
        private readonly AppDbContext _context;

        public GetOtBoardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetOtBoardResponseModel> Handle(GetOtBoardRequestModel request, CancellationToken cancellationToken)
        {
            var todayStart = DateTime.UtcNow.Date;

            // Every active case, plus completed ones only from today — keeps the board a "what's
            // happening now" view instead of an ever-growing history (full history lives in
            // SurgeryStatusHistory for reporting).
            var cases = await _context.SurgeryCase
                .Where(s => s.HospitalId == request.HospitalId
                         && s.StatusCode != IpdConstants.SurgeryStatus.Cancelled
                         && (s.StatusCode != IpdConstants.SurgeryStatus.Completed || s.UpdatedAt >= todayStart))
                .OrderBy(s => s.RequestedAt)
                .ToListAsync(cancellationToken);

            if (cases.Count == 0)
                return new GetOtBoardResponseModel { Cases = new() };

            var caseIds = cases.Select(c => c.SurgeryCaseId).ToList();

            var activeBookingsByCase = (await _context.OTBooking
                    .Where(b => caseIds.Contains(b.SurgeryCaseId) && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode))
                    .ToListAsync(cancellationToken))
                .GroupBy(b => b.SurgeryCaseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.ScheduledStart).First());

            var theatreIds = activeBookingsByCase.Values.Select(b => b.TheatreId).Distinct().ToList();
            var theatresById = await _context.OperationTheatre
                .Where(t => theatreIds.Contains(t.TheatreId))
                .ToDictionaryAsync(t => t.TheatreId, cancellationToken);

            var patientIds = cases.Select(c => c.PatientId).Where(p => p != null).Distinct().ToList();
            var patientsById = await _context.PatientRegistrations
                .Where(p => request.HospitalId == p.HospitalId && patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

            // Latest PreOpAssessment per case ("insert-only, latest wins" convention).
            var latestPreOpByCase = (await _context.PreOpAssessment
                    .Where(a => caseIds.Contains(a.SurgeryCaseId))
                    .ToListAsync(cancellationToken))
                .GroupBy(a => a.SurgeryCaseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AssessedAt).First());

            var checklistsByCase = await _context.SurgicalSafetyChecklist
                .Where(c => caseIds.Contains(c.SurgeryCaseId))
                .ToDictionaryAsync(c => c.SurgeryCaseId, cancellationToken);

            var items = cases.Select(c =>
            {
                activeBookingsByCase.TryGetValue(c.SurgeryCaseId, out var booking);
                EasyHMSAPI.Domain.Entities.OperationTheatre? theatre = null;
                if (booking != null) theatresById.TryGetValue(booking.TheatreId, out theatre);
                string? patientName = c.PatientId != null && patientsById.TryGetValue(c.PatientId, out var patient) ? patient.FullName : null;
                latestPreOpByCase.TryGetValue(c.SurgeryCaseId, out var preOp);
                checklistsByCase.TryGetValue(c.SurgeryCaseId, out var checklist);

                var preOpComplete = preOp != null && preOp.NpoConfirmed && preOp.AllergiesReviewed
                    && preOp.InvestigationsReviewed && preOp.ConsentConfirmed;

                return new OtBoardCaseDataModel
                {
                    SurgeryCaseId = c.SurgeryCaseId,
                    StatusCode = c.StatusCode,
                    PatientName = patientName,
                    ProcedureName = c.ProcedureName,
                    SurgeonName = c.SurgeonName,
                    SurgeryType = c.SurgeryType,
                    Urgency = c.Urgency,
                    TheatreId = booking?.TheatreId,
                    TheatreName = theatre?.TheatreName,
                    ScheduledStart = booking?.ScheduledStart,
                    ScheduledEnd = booking?.ScheduledEnd,
                    EncounterId = c.EncounterId,
                    AdmissionId = c.AdmissionId,
                    PreOpAssessmentComplete = preOpComplete,
                    SignInComplete = checklist?.SignInCompletedAt != null,
                    TimeOutComplete = checklist?.TimeOutCompletedAt != null,
                    SignOutComplete = checklist?.SignOutCompletedAt != null,
                };
            }).ToList();

            return new GetOtBoardResponseModel { Cases = items };
        }
    }
}
