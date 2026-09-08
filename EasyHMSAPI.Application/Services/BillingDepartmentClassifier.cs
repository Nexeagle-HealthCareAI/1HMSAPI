namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Rolls up the free-text, hospital-configurable BillingChargeEvent.CategoryCode into the four
    /// buckets hospital finance actually reports against: OPD, IPD, LAB, PHARMACY.
    ///
    /// CategoryCode alone can't tell OPD from IPD -- a lab test or a pharmacy dispense bills against
    /// whichever OPD/IPD encounter ordered it (see PathologyAutoBillingHelper.ResolveBillingEncounterIdAsync),
    /// so a lab charge's own encounter is typically OPD or IPD, not "LAB". LAB and PHARMACY are
    /// therefore detected first, by keyword on CategoryCode/SourceModule, since they cut across visit
    /// type; only charges that are neither lab nor pharmacy fall back to the linked Encounter's
    /// EncounterTypeCode to decide OPD vs IPD. A charge that matches none of these (an ER visit, a
    /// charge with no resolvable encounter, or an unrecognized category) lands in OTHER so the four
    /// named buckets never silently swallow revenue -- callers that only want the four named
    /// categories can filter OTHER out client-side, but it stays in the data so totals reconcile.
    /// </summary>
    public static class BillingDepartmentClassifier
    {
        public const string Opd = "OPD";
        public const string Ipd = "IPD";
        public const string Lab = "LAB";
        public const string Pharmacy = "PHARMACY";
        public const string Other = "OTHER";

        public static string Classify(string? categoryCode, string? sourceModule, string? encounterTypeCode)
        {
            var category = (categoryCode ?? string.Empty).ToUpperInvariant();
            var source = (sourceModule ?? string.Empty).ToUpperInvariant();

            if (category.Contains("LAB") || category.Contains("PATH") || category.Contains("RAD") || source.Contains("LAB"))
                return Lab;

            if (category.Contains("PHARM") || category.Contains("DRUG") || source.Contains("PHARMACY"))
                return Pharmacy;

            var encounterType = (encounterTypeCode ?? string.Empty).ToUpperInvariant();
            if (encounterType == Pharmacy) return Pharmacy; // retail counter sale, no separate patient encounter
            if (encounterType == Ipd) return Ipd;
            if (encounterType == Opd) return Opd;

            return Other;
        }
    }
}
