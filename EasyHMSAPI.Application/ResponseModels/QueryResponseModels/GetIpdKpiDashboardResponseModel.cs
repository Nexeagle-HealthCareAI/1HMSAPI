using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetIpdKpiDashboardResponseModel
    {
        public decimal CurrentBorPercent { get; set; }
        public List<BorTrendPoint> BorTrend { get; set; } = new();

        public decimal AlosDays { get; set; }
        public List<AlosTrendPoint> AlosTrend { get; set; } = new();

        public decimal AvgBedTurnaroundHours { get; set; }

        public decimal AvgDischargeTatHours { get; set; }
        public int DischargeTatSampleSize { get; set; }

        public decimal ReadmissionRatePercent { get; set; }
        public int ReadmittedCount { get; set; }
        public int TotalIndexDischarges { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BorTrendPoint
    {
        public DateTime Day { get; set; }
        public decimal BorPercent { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AlosTrendPoint
    {
        public DateTime WeekStart { get; set; }
        public decimal AvgDays { get; set; }
    }
}
