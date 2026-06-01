using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Default document-numbering formats applied to ANY hospital. When an invoice or receipt is
    /// generated and the hospital has not explicitly configured a NumberSeries (via Billing Policy →
    /// Document Sequencing), we transparently create the series with these defaults so numbering
    /// always works out of the box. The defaults mirror the Billing Policy UI:
    ///   Invoice → INV-YYYY-000001,  Receipt → RCPT-YYYY-000001.
    /// </summary>
    public static class NumberSeriesDefaults
    {
        public static (string Prefix, string YearFormat, string Separator, int PadLength) For(string? seriesCode)
        {
            if (string.Equals(seriesCode, BillingConstants.NumberSeriesCode.Receipt, StringComparison.OrdinalIgnoreCase))
                return ("RCPT", "YYYY", "-", 6);
            if (string.Equals(seriesCode, BillingConstants.NumberSeriesCode.Admission, StringComparison.OrdinalIgnoreCase))
                return ("ADM", "YYYY", "-", 6);
            if (string.Equals(seriesCode, BillingConstants.NumberSeriesCode.InterimBill, StringComparison.OrdinalIgnoreCase))
                return ("IB", "YYYY", "-", 6);
            return ("INV", "YYYY", "-", 6);
        }

        /// <summary>
        /// Returns the hospital's NumberSeries for the given code, creating it with sensible defaults
        /// (and adding it to the context, not yet saved) when none exists. Never returns null.
        /// </summary>
        public static async Task<NumberSeries> GetOrCreateAsync(
            AppDbContext context,
            Guid hospitalId,
            string seriesCode,
            string? user,
            CancellationToken cancellationToken)
        {
            var series = await context.NumberSeries
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId && s.SeriesCode == seriesCode, cancellationToken);

            if (series != null) return series;

            var (prefix, yearFormat, separator, padLength) = For(seriesCode);
            series = new NumberSeries
            {
                SeriesId = Guid.NewGuid(),
                HospitalId = hospitalId,
                SeriesCode = seriesCode,
                Prefix = prefix,
                YearFormat = yearFormat,
                Separator = separator,
                PadLength = padLength,
                CurrentValue = 0,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = user
            };
            context.NumberSeries.Add(series);
            return series;
        }
    }
}
