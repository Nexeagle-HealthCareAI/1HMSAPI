using EasyHMSAPI.Data.Constants;

namespace EasyHMSAPI.Data.Services
{
    /// <summary>
    /// Resolves whether a charge line is GST-exempt (and at what rate if not), given the room/ward
    /// context a plain per-item GstSlabPercent can't express on its own. Pure/stateless — sits
    /// alongside GstTaxComputer, which stays responsible for splitting a resolved rate into
    /// CGST/SGST/IGST; this class only decides WHETHER and AT WHAT RATE tax applies.
    ///
    /// Rule precedence (first match wins), from the room-rent GST table:
    ///   1. Category is a room/bed charge AND ward is ICU-family (ICU/CCU/ICCU/NICU/PICU/HDU)
    ///      -> always exempt, regardless of tariff.
    ///   2. isBundled (a package rate, not itemised) -> exempt on the room component. Always false
    ///      this phase (package billing is deferred) — the parameter exists so package support can
    ///      slot in later without a signature change.
    ///   3. Category is a room/bed charge AND roomDailyRate > the statutory threshold (Rs.5,000/day)
    ///      -> taxable at RoomRentGstPercent (5%), no ITC.
    ///   4. Category is a room/bed charge AND roomDailyRate <= threshold -> exempt.
    ///   5. sourceModule is PHARMACY_IPD (pharmacy issued as part of IPD treatment) -> exempt.
    ///   6. sourceModule is PHARMACY_COUNTER (retail/OPD/post-discharge pharmacy) -> taxable at the
    ///      item's own ChargeMaster GstSlabPercent (falls through, no override).
    ///   7. Anything else -> falls through unchanged to the item's own ChargeMaster.IsTaxable/GstSlabPercent.
    /// </summary>
    public static class GstResolver
    {
        public const decimal RoomRentThreshold = 5000m;
        public const decimal RoomRentGstPercent = 5m;

        public sealed record GstTreatment(bool IsExempt, decimal? EffectiveGstRatePercent, bool NoInputTaxCredit, string Reason);

        /// <summary>
        /// categoryCode: the charge line's ChargeMaster.CategoryCode (case-insensitive "BED"/"ROOM" is
        /// treated as a room/bed charge; everything else is a non-room service/item).
        /// wardType: the admission's active bed's WardType, or null if not IPD/no active bed.
        /// roomDailyRate: the active bed's effective daily rate (DailyRateSnapshot or ward rate), or null.
        /// sourceModule: BillingConstants.SourceModule for this line, or null.
        /// isBundled: true only when the line is part of a fixed-price package — always false this
        /// phase (package billing deferred), kept as a parameter for forward compatibility.
        /// itemGstSlabPercent/itemIsTaxable: the charge item's own configured tax treatment, used
        /// as the fallback when none of the room/pharmacy rules apply.
        /// </summary>
        public static GstTreatment Resolve(
            string? categoryCode,
            string? wardType,
            decimal? roomDailyRate,
            string? sourceModule,
            bool isBundled,
            decimal? itemGstSlabPercent,
            bool itemIsTaxable)
        {
            var isRoomCharge = IsRoomCategory(categoryCode);

            if (isRoomCharge && IpdConstants.WardType.IsIcuFamily(wardType))
                return new GstTreatment(true, null, false, "ICU/CCU/ICCU/NICU/PICU/HDU room — always exempt regardless of tariff.");

            if (isBundled)
                return new GstTreatment(true, null, false, "Bundled package rate — room component not itemised, GST does not apply.");

            if (isRoomCharge)
            {
                if (roomDailyRate.HasValue && roomDailyRate.Value > RoomRentThreshold)
                    return new GstTreatment(false, RoomRentGstPercent, true, $"Non-ICU room rent > Rs.{RoomRentThreshold:N0}/day — 5% GST, no ITC.");
                return new GstTreatment(true, null, false, $"Non-ICU room rent <= Rs.{RoomRentThreshold:N0}/day — exempt.");
            }

            if (string.Equals(sourceModule, BillingConstants.SourceModule.PharmacyIpd, StringComparison.OrdinalIgnoreCase))
                return new GstTreatment(true, null, false, "Pharmacy issued as part of IPD treatment — exempt.");

            if (string.Equals(sourceModule, BillingConstants.SourceModule.PharmacyCounter, StringComparison.OrdinalIgnoreCase))
                return new GstTreatment(!itemIsTaxable, itemIsTaxable ? itemGstSlabPercent : null, false, "Retail/counter pharmacy — taxable at drug rate.");

            // No room/pharmacy rule applies — leave the item's own configured tax treatment untouched.
            return new GstTreatment(!itemIsTaxable, itemIsTaxable ? itemGstSlabPercent : null, false, "No room/pharmacy override — item's own configured GST treatment applies.");
        }

        private static bool IsRoomCategory(string? categoryCode) =>
            !string.IsNullOrWhiteSpace(categoryCode)
            && (categoryCode.Trim().Equals("BED", StringComparison.OrdinalIgnoreCase)
                || categoryCode.Trim().Equals("ROOM", StringComparison.OrdinalIgnoreCase));
    }
}
