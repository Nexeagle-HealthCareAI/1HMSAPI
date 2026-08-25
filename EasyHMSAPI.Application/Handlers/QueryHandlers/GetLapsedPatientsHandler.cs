using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Lapsed Patients / Re-engagement: Stage 1 deterministically finds patients who used to visit
    /// regularly but haven't returned within their own rhythm (PatientChurnAnalyzer, no AI
    /// involved); Stage 2 asks Groq for a generic outreach message template from the aggregate
    /// count only (IPatientChurnInsightService) -- Groq never sees a patient name.
    /// </summary>
    public class GetLapsedPatientsHandler : IRequestHandler<GetLapsedPatientsRequestModel, GetLapsedPatientsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IPatientChurnInsightService _insightService;

        public GetLapsedPatientsHandler(AppDbContext context, IPatientChurnInsightService insightService)
        {
            _context = context;
            _insightService = insightService;
        }

        public async Task<GetLapsedPatientsResponseModel> Handle(GetLapsedPatientsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // MergedIntoPatientId != null means this record was superseded by a canonical UHID
                // elsewhere -- counting it here would misreport an active patient as lapsed.
                var patients = await _context.PatientRegistrations
                    .AsNoTracking()
                    .Where(p => p.HospitalId == request.HospitalId && p.MergedIntoPatientId == null && !string.IsNullOrEmpty(p.PatientId))
                    .Select(p => new { p.PatientId, p.FullName, p.MarketingConsent })
                    .ToListAsync(cancellationToken);

                var patientIds = patients.Select(p => p.PatientId!).ToList();

                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId && a.PatientId != null && patientIds.Contains(a.PatientId))
                    .Select(a => new { a.PatientId, a.DoctorId, a.ApptDate })
                    .ToListAsync(cancellationToken);

                var visitsByPatient = appointments.GroupBy(a => a.PatientId!).ToDictionary(g => g.Key, g => g.Select(a => a.ApptDate).ToList());

                var doctorIds = appointments.Select(a => a.DoctorId).Distinct().ToList();
                var doctorSpecialtyNames = await (
                        from ds in _context.DoctorSpecializations
                        join s in _context.Specializations on ds.SpecializationID equals s.SpecializationID
                        where doctorIds.Contains(ds.DoctorID)
                        select new { ds.DoctorID, SpecialtyName = s.Name })
                    .ToListAsync(cancellationToken);
                var specialtyByDoctor = doctorSpecialtyNames.GroupBy(x => x.DoctorID).ToDictionary(g => g.Key, g => g.First().SpecialtyName);

                var visitHistories = patients
                    .Where(p => !string.IsNullOrEmpty(p.PatientId) && visitsByPatient.ContainsKey(p.PatientId!))
                    .Select(p => new PatientVisitHistory(p.PatientId!, p.FullName ?? string.Empty, p.MarketingConsent, visitsByPatient[p.PatientId!]))
                    .ToList();

                var lapsed = PatientChurnAnalyzer.FindLapsedPatients(visitHistories, DateTime.UtcNow);

                // Aggregate-only signal for Groq -- specialties lapsed patients most commonly used
                // to visit, never a patient name or any other per-patient data.
                var lapsedPatientIds = lapsed.Select(l => l.PatientId).ToHashSet();
                var topSpecialties = appointments
                    .Where(a => lapsedPatientIds.Contains(a.PatientId!) && specialtyByDoctor.ContainsKey(a.DoctorId))
                    .GroupBy(a => specialtyByDoctor[a.DoctorId])
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();

                var summary = new PatientChurnSummary(lapsed.Count, lapsed.Count(l => l.MarketingConsent), topSpecialties);
                var narrative = await _insightService.GenerateInsightsAsync(summary);

                var page = Math.Max(1, request.Page);
                var limit = Math.Max(1, request.Limit);
                var paged = lapsed.Skip((page - 1) * limit).Take(limit).ToList();

                return new GetLapsedPatientsResponseModel
                {
                    Success = true,
                    Message = "Lapsed patients retrieved.",
                    Data = new LapsedPatientsData
                    {
                        TotalCount = lapsed.Count,
                        Page = page,
                        Limit = limit,
                        Outlook = narrative.Outlook,
                        SuggestedOutreachMessage = narrative.SuggestedOutreachMessage,
                        Patients = paged.Select(l => new LapsedPatientItem
                        {
                            PatientId = l.PatientId,
                            FullName = l.FullName,
                            MarketingConsent = l.MarketingConsent,
                            VisitCount = l.VisitCount,
                            LastVisitDate = l.LastVisitDate,
                            DaysSinceLastVisit = l.DaysSinceLastVisit,
                            AverageGapDays = l.AverageGapDays
                        }).ToList()
                    }
                };
            }
            catch (Exception)
            {
                return new GetLapsedPatientsResponseModel { Success = false, Message = "Error retrieving lapsed patients." };
            }
        }
    }
}
