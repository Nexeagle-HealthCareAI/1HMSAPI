using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Data.Constants
{
    [ExcludeFromCodeCoverage]
    public static class BillingConstants
    {
        public static class ChargeEventStatus
        {
            public const string Draft = "DRAFT";
            public const string Posted = "POSTED";
            public const string Invoiced = "INVOICED";
            public const string Void = "VOID";
        }

        public static class EncounterStatus
        {
            public const string Open = "OPEN";
            public const string Finalized = "FINALIZED";
            public const string Cancelled = "CANCELLED";
        }

        public static class InvoiceStatus
        {
            public const string Draft = "DRAFT";
            public const string Finalized = "FINALIZED";
            public const string Cancelled = "CANCELLED";
        }

        public static class SourceModule
        {
            public const string Manual = "MANUAL";
            public const string Opd = "OPD";
            public const string Ipd = "IPD";
            public const string LabPath = "LAB_PATH";
            public const string LabRad = "LAB_RAD";
            public const string PharmacyIpd = "PHARMACY_IPD";
            public const string PharmacyCounter = "PHARMACY_COUNTER";
        }

        public static class EncounterType
        {
            public const string Opd = "OPD";
            public const string Ipd = "IPD";
            public const string Er = "ER";
            public const string Lab = "LAB";
            public const string Pharmacy = "PHARMACY";
        }
    }
}
