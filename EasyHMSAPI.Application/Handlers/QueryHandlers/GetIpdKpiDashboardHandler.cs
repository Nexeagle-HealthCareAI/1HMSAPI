using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// IPD operations KPI dashboard — one cohesive handler for all 5 metrics (mirrors
    /// GetHospitalOverallAnalysisHandler's "one big handler per dashboard" shape). Fetches raw
    /// rows here; all actual computation is delegated to IpdKpiCalculator.
    /// </summary>
    public class GetIpdKpiDashboardHandler : IRequestHandler<GetIpdKpiDashboardRequestModel, GetIpdKpiDashboardResponseModel>
    {
        private readonly AppDbContext _context;

        public GetIpdKpiDashboardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetIpdKpiDashboardResponseModel> Handle(GetIpdKpiDashboardRequestModel request, CancellationToken cancellationToken)
        {
            var fromDate = request.FromDate.Date;
            var toDate = request.ToDate.Date;

            // ── BOR + bed turnaround: needs each bed's FULL assignment history for correct pairing/overlap, not date-limited ──
            var totalActiveBeds = await _context.BedMaster
                .CountAsync(b => b.HospitalId == request.HospitalId && b.IsActive, cancellationToken);

            var spans = await _context.BedAssignment
                .Where(a => a.HospitalId == request.HospitalId)
                .Select(a => new IpdKpiCalculator.BedSpan(a.BedId, a.AssignedAt, a.ReleasedAt))
                .ToListAsync(cancellationToken);

            var borSeries = IpdKpiCalculator.ComputeBorSeries(spans, totalActiveBeds, fromDate, toDate);
            var currentBor = borSeries.Count > 0 ? borSeries[^1].BorPercent : 0m;
            var avgTurnaround = IpdKpiCalculator.ComputeBedTurnaroundHours(spans, fromDate, toDate.AddDays(1).AddTicks(-1));

            // ── ALOS: every admission discharged in the window, any terminal status ──
            var discharged = await _context.Admission
                .Where(a => a.HospitalId == request.HospitalId && a.DischargedAt != null
                    && a.DischargedAt >= fromDate && a.DischargedAt < toDate.AddDays(1))
                .Select(a => new { a.AdmittedAt, DischargedAt = a.DischargedAt!.Value })
                .ToListAsync(cancellationToken);
            var (alosDays, alosTrend) = IpdKpiCalculator.ComputeAlos(discharged.Select(d => (d.AdmittedAt, d.DischargedAt)).ToList());

            // ── Discharge TAT: DISCHARGE_INITIATED -> first later terminal transition, terminal anchored in window ──
            var relevantToStatuses = new[] { IpdConstants.AdmissionStatus.DischargeInitiated }
                .Concat(IpdConstants.AdmissionStatus.Terminal).ToArray();
            var history = await _context.AdmissionStatusHistory
                .Where(h => h.HospitalId == request.HospitalId && relevantToStatuses.Contains(h.ToStatus))
                .Select(h => new { h.AdmissionId, h.ToStatus, h.ChangedAt })
                .ToListAsync(cancellationToken);

            var tatPairs = new List<(DateTime InitiatedAt, DateTime TerminalAt)>();
            foreach (var group in history.GroupBy(h => h.AdmissionId))
            {
                var initiated = group.Where(h => h.ToStatus == IpdConstants.AdmissionStatus.DischargeInitiated)
                    .OrderBy(h => h.ChangedAt).FirstOrDefault();
                if (initiated == null) continue;

                var terminal = group.Where(h => IpdConstants.AdmissionStatus.Terminal.Contains(h.ToStatus) && h.ChangedAt > initiated.ChangedAt)
                    .OrderBy(h => h.ChangedAt).FirstOrDefault();
                if (terminal == null) continue;

                if (terminal.ChangedAt >= fromDate && terminal.ChangedAt < toDate.AddDays(1))
                    tatPairs.Add((initiated.ChangedAt, terminal.ChangedAt));
            }
            var avgTat = IpdKpiCalculator.ComputeDischargeTatHours(tatPairs);

            // ── Readmission rate: clean DISCHARGED index population in window, checked against each patient's full admission history ──
            var indexDischarges = await _context.Admission
                .Where(a => a.HospitalId == request.HospitalId && a.StatusCode == IpdConstants.AdmissionStatus.Discharged
                    && a.DischargedAt != null && a.DischargedAt >= fromDate && a.DischargedAt < toDate.AddDays(1))
                .Select(a => new { a.PatientId, DischargedAt = a.DischargedAt!.Value })
                .ToListAsync(cancellationToken);

            var indexPatientIds = indexDischarges.Select(d => d.PatientId).Distinct().ToList();
            var laterAdmissionsByPatient = await _context.Admission
                .Where(a => a.HospitalId == request.HospitalId && indexPatientIds.Contains(a.PatientId))
                .GroupBy(a => a.PatientId)
                .Select(g => new { PatientId = g.Key, AdmittedDates = g.Select(a => a.AdmittedAt).ToList() })
                .ToDictionaryAsync(g => g.PatientId, g => g.AdmittedDates, cancellationToken);

            var (readmitted, totalIndex, readmissionRate) = IpdKpiCalculator.ComputeReadmissionRate(
                indexDischarges.Select(d => (d.PatientId, d.DischargedAt)).ToList(), laterAdmissionsByPatient);

            return new GetIpdKpiDashboardResponseModel
            {
                CurrentBorPercent = currentBor,
                BorTrend = borSeries.Select(p => new BorTrendPoint { Day = p.Day, BorPercent = p.BorPercent }).ToList(),
                AlosDays = alosDays,
                AlosTrend = alosTrend.Select(p => new AlosTrendPoint { WeekStart = p.WeekStart, AvgDays = p.AvgDays }).ToList(),
                AvgBedTurnaroundHours = avgTurnaround,
                AvgDischargeTatHours = avgTat,
                DischargeTatSampleSize = tatPairs.Count,
                ReadmissionRatePercent = readmissionRate,
                ReadmittedCount = readmitted,
                TotalIndexDischarges = totalIndex,
            };
        }
    }
}
