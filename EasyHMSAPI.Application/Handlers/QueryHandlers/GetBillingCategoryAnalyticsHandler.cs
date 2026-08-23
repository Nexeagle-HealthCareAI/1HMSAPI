using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Daily revenue-vs-expense summary, broken down by category (CategoryCode -- the finer
    /// service-type bucket: CONSULT/LAB/RADIOLOGY/PHARMACY/BED/PROCEDURE/etc. -- rather than
    /// SourceModule, which only distinguishes OPD/IPD/pharmacy-channel context). Filterable to a
    /// single day, a date range, or all-time when both dates are omitted.
    /// </summary>
    public class GetBillingCategoryAnalyticsHandler : IRequestHandler<GetBillingCategoryAnalyticsRequestModel, GetBillingCategoryAnalyticsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBillingCategoryAnalyticsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBillingCategoryAnalyticsResponseModel> Handle(GetBillingCategoryAnalyticsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // EndDate is inclusive of its whole calendar day regardless of any time component.
                var startInclusive = request.StartDate?.Date;
                var endExclusive = request.EndDate?.Date.AddDays(1);

                var charges = await _context.BillingChargeEvent
                    .Where(c => c.HospitalId == request.HospitalId && c.StatusCode == BillingConstants.ChargeEventStatus.Posted)
                    .Where(c => startInclusive == null || c.ServiceDate >= startInclusive)
                    .Where(c => endExclusive == null || c.ServiceDate < endExclusive)
                    .Select(c => new { c.CategoryCode, c.ServiceDate, c.NetAmount })
                    .ToListAsync(cancellationToken);

                var expenses = await _context.Expenses
                    .Where(e => e.HospitalId == request.HospitalId)
                    .Where(e => startInclusive == null || e.ExpenseDate >= startInclusive)
                    .Where(e => endExclusive == null || e.ExpenseDate < endExclusive)
                    .Select(e => new { e.CategoryCode, e.ExpenseDate, e.Amount })
                    .ToListAsync(cancellationToken);

                var revenueByCategory = charges
                    .GroupBy(c => string.IsNullOrWhiteSpace(c.CategoryCode) ? "OTHER" : c.CategoryCode!)
                    .Select(g => new CategoryBreakdownItem { CategoryCode = g.Key, Amount = g.Sum(c => c.NetAmount), Count = g.Count() })
                    .OrderByDescending(c => c.Amount)
                    .ToList();

                var expenseByCategory = expenses
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.CategoryCode) ? "OTHER" : e.CategoryCode)
                    .Select(g => new CategoryBreakdownItem { CategoryCode = g.Key, Amount = g.Sum(e => e.Amount), Count = g.Count() })
                    .OrderByDescending(c => c.Amount)
                    .ToList();

                var revenueByDay = charges.GroupBy(c => c.ServiceDate.Date).ToDictionary(g => g.Key, g => g.Sum(c => c.NetAmount));
                var expenseByDay = expenses.GroupBy(e => e.ExpenseDate.Date).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
                var allDays = revenueByDay.Keys.Union(expenseByDay.Keys).OrderBy(d => d).ToList();
                var dailyTrend = allDays.Select(d => new DailyTrendPoint
                {
                    Date = d,
                    Revenue = revenueByDay.TryGetValue(d, out var r) ? r : 0m,
                    Expense = expenseByDay.TryGetValue(d, out var ex) ? ex : 0m
                }).ToList();

                var totalRevenue = revenueByCategory.Sum(c => c.Amount);
                var totalExpense = expenseByCategory.Sum(c => c.Amount);

                return new GetBillingCategoryAnalyticsResponseModel
                {
                    Success = true,
                    Message = "Billing analytics retrieved successfully.",
                    Data = new BillingCategoryAnalyticsData
                    {
                        TotalRevenue = totalRevenue,
                        TotalExpense = totalExpense,
                        NetAmount = totalRevenue - totalExpense,
                        RevenueByCategory = revenueByCategory,
                        ExpenseByCategory = expenseByCategory,
                        DailyTrend = dailyTrend
                    }
                };
            }
            catch (Exception)
            {
                return new GetBillingCategoryAnalyticsResponseModel { Success = false, Message = "Error retrieving billing analytics." };
            }
        }
    }
}
