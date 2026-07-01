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

        /// <summary>MAR — action a nurse can record against a scheduled dose slot. Persisted
        /// values; see MarSlotStatus for the larger set of computed-only, read-side statuses.</summary>
        public static class MedicationActionStatus
        {
            public const string Administered = "ADMINISTERED";
            public const string Held = "HELD";
            public const string Refused = "REFUSED";
            public const string PatientNotAvailable = "PATIENT_NOT_AVAILABLE";

            public static readonly string[] All = { Administered, Held, Refused, PatientNotAvailable };
        }

        /// <summary>MAR — the full set of statuses a computed dose slot can show on the grid. The
        /// first four mirror MedicationActionStatus (an administration row exists); the rest are
        /// derived purely by comparing the computed schedule against "now" when no matching row
        /// exists yet — never persisted (see MarScheduleCalculator/GetMarGridHandler).</summary>
        public static class MarSlotStatus
        {
            public const string Administered = "ADMINISTERED";
            public const string Held = "HELD";
            public const string Refused = "REFUSED";
            public const string PatientNotAvailable = "PATIENT_NOT_AVAILABLE";
            public const string Pending = "PENDING";     // due time is more than the "upcoming" window away
            public const string Due = "DUE";             // within the due window, not yet acted on
            public const string Overdue = "OVERDUE";     // past due time but inside the grace window
            public const string Missed = "MISSED";       // past the grace window, never acted on
        }

        /// <summary>MAR — fixed frequency codes for Medication CPOE orders (replaces free-text
        /// Frequency going forward). Existing free-text values on orders placed before this phase
        /// are left as-is; MAR's schedule computation simply can't produce slots for those lines
        /// (no matching FrequencyCode), so nurses fall back to ad-hoc/PRN-style logging for them
        /// (see MarScheduleCalculator/GetMarGridHandler remarks).</summary>
        public static class MedicationFrequency
        {
            public const string Stat = "STAT";
            public const string Od = "OD";
            public const string Bd = "BD";
            public const string Tds = "TDS";
            public const string Qid = "QID";
            public const string Q4h = "Q4H";
            public const string Q6h = "Q6H";
            public const string Q8h = "Q8H";
            public const string Q12h = "Q12H";
            public const string Sos = "SOS";   // PRN — administered ad-hoc only, no pre-scheduled slots

            public static readonly string[] All = { Stat, Od, Bd, Tds, Qid, Q4h, Q6h, Q8h, Q12h, Sos };

            // Fixed ward clock-time slots (IST, hospital routine), used only for the "clock" codes
            // (OD/BD/TDS/QID). Rolling-interval codes (Q4H/Q6H/Q8H/Q12H) and STAT/SOS are handled
            // separately by MarScheduleCalculator — see its remarks for the full algorithm.
            public static readonly IReadOnlyDictionary<string, TimeSpan[]> ClockTimes = new Dictionary<string, TimeSpan[]>
            {
                [Od] = new[] { new TimeSpan(8, 0, 0) },
                [Bd] = new[] { new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0) },
                [Tds] = new[] { new TimeSpan(8, 0, 0), new TimeSpan(14, 0, 0), new TimeSpan(20, 0, 0) },
                [Qid] = new[] { new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(16, 0, 0), new TimeSpan(20, 0, 0) },
            };

            // Rolling-interval codes: hours between doses, starting from the order's first-dose
            // time (OrderedAt).
            public static readonly IReadOnlyDictionary<string, int> IntervalHours = new Dictionary<string, int>
            {
                [Q4h] = 4,
                [Q6h] = 6,
                [Q8h] = 8,
                [Q12h] = 12,
            };
        }
    }
}
