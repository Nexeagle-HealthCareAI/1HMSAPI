using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Data.Services
{
    /// <summary>
    /// Pure GST tax math. Splits a gross-or-net line amount into CGST + SGST (intra-state) or IGST (inter-state).
    /// All inputs and outputs are amounts in the same currency unit (₹).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class GstTaxComputer
    {
        public sealed record GstLineResult(
            decimal TaxableAmount,
            decimal CgstAmount,
            decimal SgstAmount,
            decimal IgstAmount,
            decimal TaxAmount,
            decimal LineTotal
        );

        public static GstLineResult Compute(
            decimal amount,
            decimal? gstRatePercent,
            bool taxInclusive,
            bool isInterState,
            string rounding = "ROUND")
        {
            var rate = gstRatePercent ?? 0m;
            if (rate < 0) rate = 0;

            decimal taxable;
            decimal totalTax;

            if (rate == 0m)
            {
                taxable = amount;
                totalTax = 0m;
            }
            else if (taxInclusive)
            {
                var factor = 1m + (rate / 100m);
                taxable = Round(amount / factor, rounding);
                totalTax = Round(amount - taxable, rounding);
            }
            else
            {
                taxable = amount;
                totalTax = Round(amount * rate / 100m, rounding);
            }

            decimal cgst = 0m, sgst = 0m, igst = 0m;
            if (totalTax > 0m)
            {
                if (isInterState)
                {
                    igst = totalTax;
                }
                else
                {
                    cgst = Round(totalTax / 2m, rounding);
                    sgst = totalTax - cgst;
                }
            }

            var lineTotal = taxInclusive ? amount : taxable + totalTax;
            return new GstLineResult(taxable, cgst, sgst, igst, totalTax, lineTotal);
        }

        /// <summary>Whether supplier and patient are in different states (and hence IGST applies).</summary>
        public static bool IsInterState(string? supplierStateCode, string? placeOfSupplyStateCode)
        {
            if (string.IsNullOrWhiteSpace(supplierStateCode) || string.IsNullOrWhiteSpace(placeOfSupplyStateCode))
                return false;
            return !string.Equals(supplierStateCode.Trim(), placeOfSupplyStateCode.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static decimal Round(decimal value, string mode) => mode?.Trim().ToUpperInvariant() switch
        {
            "FLOOR" => Math.Floor(value * 100m) / 100m,
            "CEIL"  => Math.Ceiling(value * 100m) / 100m,
            _       => Math.Round(value, 2, MidpointRounding.AwayFromZero),
        };
    }
}
