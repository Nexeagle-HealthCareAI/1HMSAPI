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
    /// AI Patient Volume Forecast: Stage 1 computes real trend/projection numbers from a hospital's
    /// full appointment history (PatientVolumeTrendCalculator, no AI involved) -- day-of-week and
    /// short-term trend factors stay recency-scoped internally, only month-of-year seasonality
    /// benefits from the full history, see that class's doc comment. Stage 2 asks Groq to narrate
    /// those already-computed numbers into an outlook sentence and a handful of insights
    /// (IPatientVolumeInsightService) -- Groq never invents the figures themselves.
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
                var today = DateTime.UtcNow.Date;

                // No date filter here -- month-of-year seasonality needs as much history as the
                // hospital has (see PatientVolumeTrendCalculator). Mirrors the same no-cutoff
                // pattern GetHospitalOverallAnalysisHandler already uses for a hospital's full
                // appointment history.
                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId)
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

                // Zero-filled from the hospital's own earliest appointment -- a 2-month-old hospital
                // gets a 2-month series, a 5-year-old one gets 5 years, never a fixed arbitrary range.
                var earliestDate = appointments.Count > 0 ? appointments.Min(a => a.ApptDate.Date) : today;
                var totalDaySpan = (today - earliestDate).Days + 1;
                var allDays = Enumerable.Range(0, totalDaySpan)
                    .Select(i => earliestDate.AddDays(i))
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

                var trend = PatientVolumeTrendCalculator.Compute(allDays, appointmentsBySpecialty);

                // Doctor load forecast: same Compute() call, run once per doctor over that doctor's
                // own zero-filled full history -- reuses the already-tested calculator as-is. A
                // recently-joined doctor naturally has a short series and every seasonal factor
                // gracefully falls back to neutral, no special-casing needed.
                var doctorLoadForecast = new List<DoctorLoadForecastEntry>();
                foreach (var doctorId in doctorIds)
                {
                    if (!doctors.TryGetValue(doctorId, out var doctor)) continue;
                    var doctorName = doctor?.User?.UserProfiles?.FirstOrDefault()?.FullName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(doctorName)) continue;

                    var doctorAppointments = appointments.Where(a => a.DoctorId == doctorId).ToList();
                    var doctorByDay = doctorAppointments
                        .GroupBy(a => a.ApptDate.Date)
                        .ToDictionary(g => g.Key, g => g.Count());
                    var doctorEarliest = doctorAppointments.Min(a => a.ApptDate.Date);
                    var doctorDaySpan = (today - doctorEarliest).Days + 1;
                    var doctorDaily = Enumerable.Range(0, doctorDaySpan)
                        .Select(i => doctorEarliest.AddDays(i))
                        .Select(d => new DailyPatientCount(d, doctorByDay.TryGetValue(d, out var c) ? c : 0, 0))
                        .ToList();

                    var doctorTrend = PatientVolumeTrendCalculator.Compute(doctorDaily, new Dictionary<string, List<(DateTime, int)>>());
                    doctorLoadForecast.Add(new DoctorLoadForecastEntry(
                        doctorId,
                        doctorName,
                        doctorTrend.PredictedNext30DayAppointments,
                        doctorTrend.MonthOverMonthAppointmentChangePercent,
                        PatientVolumeTrendCalculator.IsOverloaded(doctorTrend.PredictedNext30DayAppointments, doctorTrend.Avg30DayAppointments)
                    ));
                }
                doctorLoadForecast = doctorLoadForecast.OrderByDescending(d => d.PredictedNext30DayAppointments).Take(5).ToList();

                // Anomaly watch deliberately stays scoped to the last ~90 days (12 weekly buckets) --
                // "is this week unusual vs. recently normal" is the wrong question to ask against a
                // multi-year baseline. Derived from the same already-fetched appointments, not a
                // second query.
                var anomalyCutoff = today.AddDays(-90);
                var operationalByDay = appointments
                    .Where(a => a.ApptDate.Date >= anomalyCutoff)
                    .GroupBy(a => a.ApptDate.Date)
                    .ToDictionary(
                        g => g.Key,
                        g => (
                            NoShow: g.Count(a => a.ApptDate.Date < today && a.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired),
                            Cancelled: g.Count(a => a.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                        ));
                var operationalDays = Enumerable.Range(0, 90)
                    .Select(i => anomalyCutoff.AddDays(i))
                    .Select(d =>
                    {
                        var total = byDay.TryGetValue(d, out var c) ? c.TotalAppointments : 0;
                        var (noShow, cancelled) = operationalByDay.TryGetValue(d, out var o) ? o : (0, 0);
                        return new DailyOperationalStats(d, total, noShow, cancelled);
                    })
                    .ToList();
                var anomalies = KpiAnomalyDetector.DetectAnomalies(operationalDays);

                var narrative = await _insightService.GenerateInsightsAsync(new PatientVolumeInsightContext(trend, doctorLoadForecast, anomalies));

                // Only the recent slice goes back over the wire for charting -- the frontend chart
                // shows the last 30 days regardless of how much history fed the seasonal math.
                var historicalTrendForResponse = allDays.Count > 90 ? allDays.Skip(allDays.Count - 90).ToList() : allDays;

                return new GetPatientVolumeForecastResponseModel
                {
                    Success = true,
                    Message = "Patient volume forecast generated.",
                    Data = new PatientVolumeForecastData
                    {
                        PredictedNext30DayAppointments = trend.PredictedNext30DayAppointments,
                        PredictedNext30DayUniquePatients = trend.PredictedNext30DayUniquePatients,
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
                            PredictedNext30DayAppointments = d.PredictedNext30DayAppointments,
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
                        MonthlySeasonalFactors = trend.MonthlySeasonalFactors.Select(f => new MonthlySeasonalFactorItem
                        {
                            Month = f.Month,
                            MonthName = f.MonthName,
                            Index = f.Index,
                            IsNotable = f.IsNotable
                        }).ToList(),
                        HistoricalTrend = historicalTrendForResponse.Select(d => new PatientVolumeTrendPoint { Date = d.Date, TotalAppointments = d.TotalAppointments, UniquePatients = d.UniquePatients }).ToList(),
                        ProjectedTrend = trend.ProjectedNext30Days.Select(d => new PatientVolumeTrendPoint { Date = d.Date, TotalAppointments = d.TotalAppointments, UniquePatients = d.UniquePatients }).ToList()
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
