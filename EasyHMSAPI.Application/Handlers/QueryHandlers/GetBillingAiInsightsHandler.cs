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
    /// "Nexeagle AI Predictive Analysis": Stage 1 computes real trend/projection numbers from the
    /// last 90 days of billing history (BillingTrendCalculator, no AI involved); Stage 2 asks Groq
    /// to narrate those already-computed numbers into an outlook sentence and a handful of
    /// insights (IBillingInsightService) -- Groq never invents the figures themselves.
    /// </summary>
    public class GetBillingAiInsightsHandler : IRequestHandler<GetBillingAiInsightsRequestModel, GetBillingAiInsightsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBillingInsightService _insightService;

        public GetBillingAiInsightsHandler(AppDbContext context, IBillingInsightService insightService)
        {
            _context = context;
            _insightService = insightService;
        }

        public async Task<GetBillingAiInsightsResponseModel> Handle(GetBillingAiInsightsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var cutoff = DateTime.UtcNow.Date.AddDays(-90);

                var charges = await _context.BillingChargeEvent
                    .Where(c => c.HospitalId == request.HospitalId
                             && c.StatusCode == BillingConstants.ChargeEventStatus.Posted
                             && c.ServiceDate >= cutoff)
                    .Select(c => new { c.CategoryCode, c.ServiceDate, c.NetAmount })
                    .ToListAsync(cancellationToken);

                var expenses = await _context.Expenses
                    .Where(e => e.HospitalId == request.HospitalId && e.ExpenseDate >= cutoff)
                    .Select(e => new { e.ExpenseDate, e.Amount })
                    .ToListAsync(cancellationToken);

                var revenueByDay = charges.GroupBy(c => c.ServiceDate.Date).ToDictionary(g => g.Key, g => g.Sum(c => c.NetAmount));
                var expenseByDay = expenses.GroupBy(e => e.ExpenseDate.Date).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

                var last90Days = Enumerable.Range(0, 90)
                    .Select(i => cutoff.AddDays(i))
                    .Select(d => new DailyAmount(d, revenueByDay.TryGetValue(d, out var r) ? r : 0m, expenseByDay.TryGetValue(d, out var e) ? e : 0m))
                    .ToList();

                var revenueByCategory = charges
                    .GroupBy(c => string.IsNullOrWhiteSpace(c.CategoryCode) ? "OTHER" : c.CategoryCode!)
                    .ToDictionary(g => g.Key, g => g.Select(c => (c.ServiceDate, c.NetAmount)).ToList());

                var trend = BillingTrendCalculator.Compute(last90Days, revenueByCategory);
                var narrative = await _insightService.GenerateInsightsAsync(trend);

                return new GetBillingAiInsightsResponseModel
                {
                    Success = true,
                    Message = "AI insights generated.",
                    Data = new BillingAiInsightsData
                    {
                        PredictedTomorrowRevenue = trend.PredictedTomorrowRevenue,
                        PredictedTomorrowExpense = trend.PredictedTomorrowExpense,
                        PredictedTomorrowNet = trend.PredictedTomorrowRevenue - trend.PredictedTomorrowExpense,
                        PredictedNext7DayRevenue = trend.PredictedNext7DayRevenue,
                        PredictedNext7DayExpense = trend.PredictedNext7DayExpense,
                        PredictedNext7DayNet = trend.PredictedNext7DayRevenue - trend.PredictedNext7DayExpense,
                        PredictedNext30DayRevenue = trend.PredictedNext30DayRevenue,
                        PredictedNext30DayExpense = trend.PredictedNext30DayExpense,
                        PredictedNext30DayNet = trend.PredictedNext30DayRevenue - trend.PredictedNext30DayExpense,
                        Avg7DayRevenue = trend.Avg7DayRevenue,
                        Avg30DayRevenue = trend.Avg30DayRevenue,
                        MonthOverMonthRevenueChangePercent = trend.MonthOverMonthRevenueChangePercent,
                        MonthOverMonthExpenseChangePercent = trend.MonthOverMonthExpenseChangePercent,
                        Outlook = narrative.Outlook,
                        Insights = narrative.Insights,
                        CategoryTrends = trend.RevenueCategoryTrends.Select(c => new CategoryTrendItem
                        {
                            CategoryCode = c.CategoryCode,
                            ChangePercent = c.ChangePercent,
                            IsLeak = c.IsLeak
                        }).ToList(),
                        HistoricalTrend = last90Days.Select(d => new AiTrendPoint { Date = d.Date, Revenue = d.Revenue, Expense = d.Expense }).ToList(),
                        ProjectedTrend = trend.ProjectedNext30Days.Select(d => new AiTrendPoint { Date = d.Date, Revenue = d.Revenue, Expense = d.Expense }).ToList()
                    }
                };
            }
            catch (Exception)
            {
                return new GetBillingAiInsightsResponseModel { Success = false, Message = "Error generating AI insights." };
            }
        }
    }
}
