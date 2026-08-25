using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// AI Patient Volume Forecast: Stage 1 computes real trend/projection numbers from the last 90
    /// days of appointment history (PatientVolumeTrendCalculator, no AI involved); Stage 2 asks
    /// Groq to narrate those already-computed numbers into an outlook sentence and a handful of
    /// insights (IPatientVolumeInsightService) -- Groq never invents the figures themselves.
    /// </summary>
    public class GetPatientVolumeForecastHandler : IRequestHandler<GetPatientVolumeForecastRequestModel, GetPatientVolumeForecastResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IPatientVolumeInsightService _insightService;

        public GetPatientVolumeForecastHandler(AppDbContext context, IPatientVolumeInsightService insightService)
        {
            _context = context;
            _insightService = insightService;
        }

        public async Task<GetPatientVolumeForecastResponseModel> Handle(GetPatientVolumeForecastRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var cutoff = DateTime.UtcNow.Date.AddDays(-90);

                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId && a.ApptDate >= cutoff)
                    .Select(a => new { a.DoctorId, a.PatientId, a.ApptDate, a.CurrentStatusCode })
                    .ToListAsync(cancellationToken);

                var doctorIds = appointments.Select(a => a.DoctorId).Distinct().ToList();
                var doctorSpecialtyNames = await (
                        from ds in _context.DoctorSpecializations
                        join s in _context.Specializations on ds.SpecializationID equals s.SpecializationID
                        where doctorIds.Contains(ds.DoctorID)
                        select new { ds.DoctorID, SpecialtyName = s.Name })
                    .ToListAsync(cancellationToken);

                var specialtyByDoctor = doctorSpecialtyNames
                    .GroupBy(x => x.DoctorID)
                    .ToDictionary(g => g.Key, g => g.First().SpecialtyName);

                var doctors = await _context.Doctors
                    .AsNoTracking()
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Include(d => d.User)
                        .ThenInclude(u => u.UserProfiles)
                    .ToDictionaryAsync(d => d.DoctorID, cancellationToken);

                var byDay = appointments.GroupBy(a => a.ApptDate.Date).ToDictionary(
                    g => g.Key,
                    g => (TotalAppointments: g.Count(), UniquePatients: g.Where(a => !string.IsNullOrEmpty(a.PatientId)).Select(a => a.PatientId).Distinct().Count()));

                var last90Days = Enumerable.Range(0, 90)
                    .Select(i => cutoff.AddDays(i))
                    .Select(d => byDay.TryGetValue(d, out var counts)
                        ? new DailyPatientCount(d, counts.TotalAppointments, counts.UniquePatients)
                        : new DailyPatientCount(d, 0, 0))
                    .ToList();

                var appointmentsBySpecialty = appointments
                    .Where(a => specialtyByDoctor.ContainsKey(a.DoctorId))
                    .GroupBy(a => specialtyByDoctor[a.DoctorId])
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(a => a.ApptDate.Date).Select(dg => (dg.Key, dg.Count())).ToList());

                var trend = PatientVolumeTrendCalculator.Compute(last90Days, appointmentsBySpecialty);

                // Doctor load forecast: same Compute() call, run once per doctor over that doctor's
                // own zero-filled 90-day series -- reuses the already-tested calculator as-is.
                var doctorLoadForecast = new List<DoctorLoadForecastEntry>();
                foreach (var doctorId in doctorIds)
                {
                    if (!doctors.TryGetValue(doctorId, out var doctor)) continue;
                    var doctorName = doctor?.User?.UserProfiles?.FirstOrDefault()?.FullName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(doctorName)) continue;

                    var doctorByDay = appointments.Where(a => a.DoctorId == doctorId)
                        .GroupBy(a => a.ApptDate.Date)
                        .ToDictionary(g => g.Key, g => g.Count());
                    var doctorDaily = Enumerable.Range(0, 90)
                        .Select(i => cutoff.AddDays(i))
                        .Select(d => new DailyPatientCount(d, doctorByDay.TryGetValue(d, out var c) ? c : 0, 0))
                        .ToList();

                    var doctorTrend = PatientVolumeTrendCalculator.Compute(doctorDaily, new Dictionary<string, List<(DateTime, int)>>());
                    doctorLoadForecast.Add(new DoctorLoadForecastEntry(
                        doctorId,
                        doctorName,
                        doctorTrend.PredictedNext7DayAppointments,
                        doctorTrend.MonthOverMonthAppointmentChangePercent,
                        PatientVolumeTrendCalculator.IsOverloaded(doctorTrend.PredictedNext7DayAppointments, doctorTrend.Avg30DayAppointments)
                    ));
                }
                doctorLoadForecast = doctorLoadForecast.OrderByDescending(d => d.PredictedNext7DayAppointments).Take(5).ToList();

                // Anomaly watch: same appointments already in hand, just tallied by status per day.
                var operationalByDay = appointments.GroupBy(a => a.ApptDate.Date).ToDictionary(
                    g => g.Key,
                    g => (
                        NoShow: g.Count(a => a.ApptDate.Date < DateTime.UtcNow.Date && a.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired),
                        Cancelled: g.Count(a => a.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                    ));
                var operationalDays = Enumerable.Range(0, 90)
                    .Select(i => cutoff.AddDays(i))
                    .Select(d =>
                    {
                        var total = byDay.TryGetValue(d, out var c) ? c.TotalAppointments : 0;
                        var (noShow, cancelled) = operationalByDay.TryGetValue(d, out var o) ? o : (0, 0);
                        return new DailyOperationalStats(d, total, noShow, cancelled);
                    })
                    .ToList();
                var anomalies = KpiAnomalyDetector.DetectAnomalies(operationalDays);

                var narrative = await _insightService.GenerateInsightsAsync(new PatientVolumeInsightContext(trend, doctorLoadForecast, anomalies));

                return new GetPatientVolumeForecastResponseModel
                {
                    Success = true,
                    Message = "Patient volume forecast generated.",
                    Data = new PatientVolumeForecastData
                    {
                        PredictedNext7DayAppointments = trend.PredictedNext7DayAppointments,
                        PredictedNext7DayUniquePatients = trend.PredictedNext7DayUniquePatients,
                        Avg7DayAppointments = trend.Avg7DayAppointments,
                        Avg30DayAppointments = trend.Avg30DayAppointments,
                        Avg7DayUniquePatients = trend.Avg7DayUniquePatients,
                        Avg30DayUniquePatients = trend.Avg30DayUniquePatients,
                        MonthOverMonthAppointmentChangePercent = trend.MonthOverMonthAppointmentChangePercent,
                        MonthOverMonthUniquePatientChangePercent = trend.MonthOverMonthUniquePatientChangePercent,
                        Outlook = narrative.Outlook,
                        Insights = narrative.Insights,
                        SpecialtyTrends = trend.SpecialtyTrends.Select(s => new SpecialtyTrendItem
                        {
                            SpecialtyName = s.SpecialtyName,
                            ChangePercent = s.ChangePercent,
                            IsSurging = s.IsSurging
                        }).ToList(),
                        DoctorLoadForecast = doctorLoadForecast.Select(d => new DoctorLoadForecastItem
                        {
                            DoctorId = d.DoctorId,
                            DoctorName = d.DoctorName,
                            PredictedNext7DayAppointments = d.PredictedNext7DayAppointments,
                            MonthOverMonthChangePercent = d.MonthOverMonthChangePercent,
                            IsOverloaded = d.IsOverloaded
                        }).ToList(),
                        Anomalies = anomalies.Select(a => new AnomalyFlagItem
                        {
                            MetricName = a.MetricName,
                            RecentValue = a.RecentValue,
                            BaselineMean = a.BaselineMean,
                            BaselineStdDev = a.BaselineStdDev,
                            ZScore = a.ZScore,
                            Direction = a.Direction
                        }).ToList(),
                        HistoricalTrend = last90Days.Select(d => new PatientVolumeTrendPoint { Date = d.Date, TotalAppointments = d.TotalAppointments, UniquePatients = d.UniquePatients }).ToList(),
                        ProjectedTrend = trend.ProjectedNext7Days.Select(d => new PatientVolumeTrendPoint { Date = d.Date, TotalAppointments = d.TotalAppointments, UniquePatients = d.UniquePatients }).ToList()
                    }
                };
            }
            catch (Exception)
            {
                return new GetPatientVolumeForecastResponseModel { Success = false, Message = "Error generating patient volume forecast." };
            }
        }
    }
}
