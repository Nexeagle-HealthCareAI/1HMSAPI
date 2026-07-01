using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Data.Constants
{
    /// <summary>
    /// IPD (in-patient) domain constants — admission &amp; bed state machines, payer branch, coverage.
    /// Kept separate from BillingConstants so the IPD spine can evolve independently.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class IpdConstants
    {
        /// <summary>Admission lifecycle. Active = ADMITTED or any pre-discharge state.</summary>
        public static class AdmissionStatus
        {
            public const string PreAdmit = "PRE_ADMIT";              // elective pre-registration
            public const string Admitted = "ADMITTED";
            public const string DischargeInitiated = "DISCHARGE_INITIATED";
            public const string DischargeBilled = "DISCHARGE_BILLED";
            public const string Discharged = "DISCHARGED";
            // Terminal exits
            public const string Lama = "LAMA";                       // left against medical advice
            public const string Dama = "DAMA";                       // discharged against medical advice
            public const string TransferredOut = "TRANSFERRED_OUT";
            public const string Expired = "EXPIRED";
            public const string Cancelled = "CANCELLED";

            // States where the patient is still in-house / occupying a bed.
            public static readonly string[] Active = { PreAdmit, Admitted, DischargeInitiated, DischargeBilled };
            // States where the admission is closed (bed released, episode over).
            public static readonly string[] Terminal = { Discharged, Lama, Dama, TransferredOut, Expired, Cancelled };
        }

        public static class BedAssignmentStatus
        {
            public const string Active = "ACTIVE";
            public const string Released = "RELEASED";
        }

        /// <summary>Bed master live status.</summary>
        public static class BedStatus
        {
            public const string Available = "AVAILABLE";
            public const string Occupied = "OCCUPIED";
            public const string Cleaning = "CLEANING";
            public const string Reserved = "RESERVED";
            public const string Blocked = "BLOCKED";
        }

        /// <summary>Payer branch — the field that drives the whole workflow.</summary>
        public static class PayerType
        {
            public const string Cash = "CASH";
            public const string Tpa = "TPA";        // insurance / third-party administrator
            public const string Scheme = "SCHEME";  // govt scheme (PM-JAY etc.)

            public static readonly string[] All = { Cash, Tpa, Scheme };
        }

        public static class CoverageStatus
        {
            public const string Pending = "PENDING";
            public const string Approved = "APPROVED";
            public const string Queried = "QUERIED";
            public const string Rejected = "REJECTED";
            public const string Enhanced = "ENHANCED";
        }

        /// <summary>CPOE — one generic order schema shared by every order type.</summary>
        public static class ClinicalOrderType
        {
            public const string Medication = "MEDICATION";
            public const string Lab = "LAB";
            public const string Radiology = "RADIOLOGY";
            public const string Procedure = "PROCEDURE";
            public const string Diet = "DIET";
            public const string Nursing = "NURSING";

            public static readonly string[] All = { Medication, Lab, Radiology, Procedure, Diet, Nursing };
        }

        public static class ClinicalOrderStatus
        {
            public const string Active = "ACTIVE";
            public const string Discontinued = "DISCONTINUED";
            public const string Completed = "COMPLETED";
        }

        public static class ClinicalOrderLineStatus
        {
            public const string Active = "ACTIVE";
            public const string Discontinued = "DISCONTINUED";
        }

        /// <summary>Order urgency — meaningful mainly for Lab/Radiology/Procedure orders.</summary>
        public static class OrderUrgency
        {
            public const string Routine = "ROUTINE";
            public const string Urgent = "URGENT";
            public const string Stat = "STAT";
        }
    }
}
