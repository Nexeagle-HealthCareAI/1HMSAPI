namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Single source of truth for "is this doctor's CMS-configured consultation-fee discount
    /// currently active" — deliberately takes raw fields rather than a Doctor entity, since every
    /// caller (GetPublicDoctorsHandler, CreateChargeEventHandler) queries these via a projected
    /// Select(), not a materialized Doctor. "Active" is always computed here, never stored as its
    /// own column, so it can't drift out of sync with DiscountPercent/DiscountStartAt/DiscountEndAt.
    /// </summary>
    public static class DoctorMarketingHelpers
    {
        public static bool IsDiscountActive(decimal? discountPercent, DateTime? discountStartAt, DateTime? discountEndAt, DateTime nowUtc)
        {
            if (discountPercent is not > 0)
                return false;
            if (discountStartAt.HasValue && nowUtc < discountStartAt.Value)
                return false;
            if (discountEndAt.HasValue && nowUtc > discountEndAt.Value)
                return false;
            return true;
        }

        public static decimal ComputeDiscountAmount(decimal fee, decimal discountPercent) =>
            Math.Round(fee * discountPercent / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
