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

        public static class PaymentType
        {
            public const string Payment = "PAYMENT";
            public const string Advance = "ADVANCE";
            public const string Refund = "REFUND";
        }

        public static class NumberSeriesCode
        {
            public const string Invoice = "INV";
            public const string Receipt = "RCPT";
            public const string Encounter = "ENC";
            public const string LabAccession = "LABACC";
            public const string RadStudy = "RADSTUDY";
            public const string Admission = "ADM";
            public const string InterimBill = "IB";
            public const string PcpndtFormF = "PCPNDT";
            public const string Mlc = "MLC";
            public const string VisitorPass = "VIS";
            public const string Indent = "INDENT";
            public const string PurchaseOrder = "PO";
            public const string Grn = "GRN";
        }

        public static class BillingActionType
        {
            public const string Finalize = "finalize";
            public const string Reopen = "reopen";
            public const string Charges = "Charges";
            public const string Payment = "Payment";
        }

        public static class DayBillStatus
        {
            public const string Closed = "CLOSED";
            public const string Reopened = "REOPENED";
        }
    }
}
