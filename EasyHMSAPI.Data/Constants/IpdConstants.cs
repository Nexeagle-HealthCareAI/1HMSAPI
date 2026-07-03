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

        /// <summary>Admission.ReferringFacilityType — which kind of outside facility referred/
        /// transferred the patient in. Soft-validated (no DB CHECK), same convention as WardType.</summary>
        public static class ReferringFacilityType
        {
            public const string Phc = "PHC";
            public const string NursingHome = "NURSING_HOME";
            public const string Hospital = "HOSPITAL";
            public const string Other = "OTHER";

            public static readonly string[] All = { Phc, NursingHome, Hospital, Other };
        }

        /// <summary>DischargeSummary.ConditionAtDischarge — exact DB CHECK set.</summary>
        public static class ConditionAtDischarge
        {
            public const string Stable = "STABLE";
            public const string Improved = "IMPROVED";
            public const string Recovered = "RECOVERED";
            public const string Referred = "REFERRED";
            public const string Lama = "LAMA";
            public const string Expired = "EXPIRED";

            public static readonly string[] All = { Stable, Improved, Recovered, Referred, Lama, Expired };
        }

        /// <summary>IRDAI discharge-process clock milestone keys — shared vocabulary between
        /// GetIrdaiDischargeClocksHandler's response and StampIrdaiMilestoneHandler's request, so
        /// the frontend never hardcodes magic strings.</summary>
        public static class IrdaiClockMilestone
        {
            public const string DischargeDecision = "DISCHARGE_DECISION";   // AdmissionStatusHistory -> DISCHARGE_INITIATED
            public const string PhysicalDischarge = "PHYSICAL_DISCHARGE";   // AdmissionStatusHistory -> terminal status
            public const string ClaimSubmitted = "CLAIM_SUBMITTED";         // AdmissionCoverage.ClaimSubmittedAt (stampable)
            public const string InsurerApproval = "INSURER_APPROVAL";       // AdmissionCoverage.InsurerApprovalAt (stampable)

            // Only these two are ever stamped directly by a user action — the other two are
            // always derived from AdmissionStatusHistory.
            public static readonly string[] Stampable = { ClaimSubmitted, InsurerApproval };
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

        public static class VitalTemperatureUnit
        {
            public const string Celsius = "C";
            public const string Fahrenheit = "F";
            public static readonly string[] All = { Celsius, Fahrenheit };
        }

        public static class FluidDirection
        {
            public const string In = "IN";
            public const string Out = "OUT";
            public static readonly string[] All = { In, Out };
        }

        /// <summary>Common FluidEntry.Subtype quick-pick values surfaced by the UI — not
        /// DB-enforced (column is free NVARCHAR(30)).</summary>
        public static class FluidSubtype
        {
            public const string Urine = "Urine";
            public const string Iv = "IV";
            public const string Oral = "Oral";
            public const string Vomitus = "Vomitus";
            public const string RtAspirate = "RT_Aspirate";
            public const string DrainA = "Drain_A";
            public const string DrainB = "Drain_B";
            public const string Stool = "Stool";

            public static readonly string[] CommonIn = { Iv, Oral };
            public static readonly string[] CommonOut = { Urine, Vomitus, RtAspirate, DrainA, DrainB, Stool };
        }

        public static class GlucoseUnit
        {
            public const string MgDl = "mg/dL";
            public const string MmolL = "mmol/L";
            public static readonly string[] All = { MgDl, MmolL };
            // 1 mmol/L glucose = 18.0182 mg/dL.
            public const decimal MmolLToMgDlFactor = 18.0182m;
        }

        public static class GlucoseMealTag
        {
            public const string Fasting = "FASTING";
            public const string PostPrandial = "POST_PRANDIAL";
            public const string Random = "RANDOM";
            public const string Bedtime = "BEDTIME";
            public static readonly string[] All = { Fasting, PostPrandial, Random, Bedtime };
        }

        /// <summary>App-computed hypo/hyper thresholds (mg/dL basis) — no DB enforcement, per
        /// create_tables_fluid_glucose.sql's own comments.</summary>
        public static class GlucoseThresholds
        {
            public const decimal HypoMgDl = 70m;
            public const decimal HyperMgDl = 180m;
        }

        /// <summary>Morse Fall Scale component value sets — exact CHECK-constrained sets from
        /// create_tables_nursing_assessment.sql.</summary>
        public static class MorseFallScale
        {
            public static readonly int[] HistoryOfFallingOptions = { 0, 25 };
            public static readonly int[] SecondaryDiagnosisOptions = { 0, 15 };
            public static readonly int[] AmbulatoryAidOptions = { 0, 15, 30 };
            public static readonly int[] IvHeparinLockOptions = { 0, 20 };
            public static readonly int[] GaitOptions = { 0, 10, 20 };
            public static readonly int[] MentalStatusOptions = { 0, 15 };
        }

        public static class MorseRisk
        {
            public const string None = "NONE";
            public const string Low = "LOW";
            public const string High = "HIGH";
            public static string FromTotal(int total) => total >= 45 ? High : total >= 25 ? Low : None;
        }

        public static class BradenRisk
        {
            public const string None = "NONE";
            public const string Mild = "MILD";
            public const string Moderate = "MODERATE";
            public const string High = "HIGH";
            public const string VeryHigh = "VERY_HIGH";
            public static string FromTotal(int total) => total <= 9 ? VeryHigh : total <= 12 ? High : total <= 14 ? Moderate : total <= 18 ? Mild : None;
        }

        public static class MustRisk
        {
            public const string Low = "LOW";
            public const string Medium = "MEDIUM";
            public const string High = "HIGH";
            public static string FromTotal(int total) => total >= 2 ? High : total == 1 ? Medium : Low;
        }

        /// <summary>Documented set for ConsentTemplate.TypeCode — the DB has no CHECK on this
        /// column (deliberately loose), so this is soft validation only, not a hard allow-list.</summary>
        public static class ConsentTypeCode
        {
            public const string GeneralAdmission = "GENERAL_ADMISSION";
            public const string Procedure = "PROCEDURE";
            public const string Radiation = "RADIATION";
            public const string IvContrast = "IV_CONTRAST";
            public const string BloodTransfusion = "BLOOD_TRANSFUSION";
            public const string Anaesthesia = "ANAESTHESIA";
            public const string Other = "OTHER";
            public static readonly string[] All = { GeneralAdmission, Procedure, Radiation, IvContrast, BloodTransfusion, Anaesthesia, Other };
        }

        public static class ShiftCode
        {
            public const string Morning = "MORNING";
            public const string Evening = "EVENING";
            public const string Night = "NIGHT";
            public static readonly string[] All = { Morning, Evening, Night };
        }

        public static class NursingCarePlanStatus
        {
            public const string Active = "ACTIVE";
            public const string Resolved = "RESOLVED";
            public const string Discontinued = "DISCONTINUED";
            public static readonly string[] All = { Active, Resolved, Discontinued };
        }

        public static class RestraintStatus
        {
            public const string Active = "ACTIVE";
            public const string Released = "RELEASED";
            public static readonly string[] All = { Active, Released };
        }

        /// <summary>Round-note 24-hour edit lock — a frontend affordance (the DB has no
        /// enforcement): once a note is older than this window, the UI offers "add addendum"
        /// instead of "edit," and the handler requires AddendumReason whenever ParentNoteId is set.</summary>
        public static class RoundNoteRules
        {
            public static readonly TimeSpan EditLockWindow = TimeSpan.FromHours(24);
        }

        // ---- Inventory ----

        public static class InventoryCategory
        {
            public const string Consumable = "CONSUMABLE";
            public const string Drug = "DRUG";
            public const string Disposable = "DISPOSABLE";
            public const string Surgical = "SURGICAL";
            public const string Implant = "IMPLANT";
            public const string Other = "OTHER";
            public static readonly string[] All = { Consumable, Drug, Disposable, Surgical, Implant, Other };
        }

        public static class InventoryMovementType
        {
            public const string Receive = "RECEIVE";
            public const string Issue = "ISSUE";
            public const string Return = "RETURN";
            public const string AdjustIn = "ADJUST_IN";
            public const string AdjustOut = "ADJUST_OUT";
            public static readonly string[] All = { Receive, Issue, Return, AdjustIn, AdjustOut };
        }

        // ---- Blood Bank ----

        public static class BloodComponent
        {
            public const string Whole = "WHOLE";
            public const string Prbc = "PRBC";
            public const string Ffp = "FFP";
            public const string Platelet = "PLATELET";
            public const string Cryo = "CRYO";
            public static readonly string[] All = { Whole, Prbc, Ffp, Platelet, Cryo };
        }

        public static class BloodGroup
        {
            public const string APos = "A_POS";
            public const string ANeg = "A_NEG";
            public const string BPos = "B_POS";
            public const string BNeg = "B_NEG";
            public const string OPos = "O_POS";
            public const string ONeg = "O_NEG";
            public const string AbPos = "AB_POS";
            public const string AbNeg = "AB_NEG";
            public static readonly string[] All = { APos, ANeg, BPos, BNeg, OPos, ONeg, AbPos, AbNeg };
        }

        public static class BloodBagStatus
        {
            public const string Available = "AVAILABLE";
            public const string Reserved = "RESERVED";
            public const string Transfused = "TRANSFUSED";
            public const string Discarded = "DISCARDED";
        }

        public static class CrossmatchResult
        {
            public const string Compatible = "COMPATIBLE";
            public const string Incompatible = "INCOMPATIBLE";
            public const string NotDone = "NOT_DONE";
        }

        public static class TransfusionReaction
        {
            public const string None = "NONE";
            public const string Mild = "MILD";
            public const string Severe = "SEVERE";
            public const string Anaphylaxis = "ANAPHYLAXIS";
            public static readonly string[] All = { None, Mild, Severe, Anaphylaxis };
        }

        // ---- Operation Theatre ----

        public static class TheatreStatus
        {
            public const string Available = "AVAILABLE";
            public const string InUse = "IN_USE";
            public const string Cleaning = "CLEANING";
            public const string Unavailable = "UNAVAILABLE";
        }

        public static class SurgeryType
        {
            public const string Elective = "ELECTIVE";
            public const string Emergency = "EMERGENCY";
            public static readonly string[] All = { Elective, Emergency };
        }

        public static class SurgeryUrgency
        {
            public const string Routine = "ROUTINE";
            public const string Urgent = "URGENT";
            public const string Emergency = "EMERGENCY";
            public static readonly string[] All = { Routine, Urgent, Emergency };
        }

        /// <summary>SurgeryCase lifecycle. Active = requested through post-op, still an open case.</summary>
        public static class SurgeryStatus
        {
            public const string Requested = "REQUESTED";
            public const string Scheduled = "SCHEDULED";
            public const string PreOp = "PRE_OP";
            public const string InTheatre = "IN_THEATRE";
            public const string PostOp = "POST_OP";
            public const string Completed = "COMPLETED";
            public const string Cancelled = "CANCELLED";

            public static readonly string[] Active = { Requested, Scheduled, PreOp, InTheatre, PostOp };
            public static readonly string[] Terminal = { Completed, Cancelled };
        }

        public static class OTBookingStatus
        {
            public const string Scheduled = "SCHEDULED";
            public const string InProgress = "IN_PROGRESS";
            public const string Completed = "COMPLETED";
            public const string Cancelled = "CANCELLED";

            // Bookings in either of these states hold the one-active-booking-per-case slot
            // (mirrors the DB's filtered unique index UX_OTB_CaseActive) and count toward the
            // theatre-overlap conflict check.
            public static readonly string[] Active = { Scheduled, InProgress };
        }

        public static class AsaGrade
        {
            public const string I = "I";
            public const string II = "II";
            public const string III = "III";
            public const string IV = "IV";
            public const string V = "V";
            public const string VI = "VI";
            public static readonly string[] All = { I, II, III, IV, V, VI };
        }

        public static class AnaesthesiaType
        {
            public const string Ga = "GA";
            public const string Spinal = "SPINAL";
            public const string Epidural = "EPIDURAL";
            public const string Local = "LOCAL";
            public const string Sedation = "SEDATION";
            public const string Regional = "REGIONAL";
            public static readonly string[] All = { Ga, Spinal, Epidural, Local, Sedation, Regional };
        }

        public static class IntraOpItemCategory
        {
            public const string Consumable = "CONSUMABLE";
            public const string Implant = "IMPLANT";
            public static readonly string[] All = { Consumable, Implant };
        }

        /// <summary>WHO 2009 Surgical Safety Checklist — fixed 3-phase item list. Item answers are
        /// persisted as a JSON blob per phase (SurgicalSafetyChecklist.SignInItemsJson etc.) against
        /// this list — not DB-enforced, soft validation only (same posture as ConsentTypeCode). Keys
        /// are stable identifiers for the JSON blob; labels are the exact WHO checklist wording.</summary>
        public static class WhoChecklistItems
        {
            public static readonly IReadOnlyList<(string Key, string Label)> SignIn = new[]
            {
                ("identity_site_procedure_consent", "Patient has confirmed identity, site, procedure, and consent"),
                ("site_marked", "Site marked / not applicable"),
                ("anaesthesia_safety_check", "Anaesthesia safety check completed"),
                ("pulse_oximeter", "Pulse oximeter on patient and functioning"),
                ("known_allergy", "Known allergy?"),
                ("difficult_airway_risk", "Difficult airway/aspiration risk? If yes, equipment/assistance available"),
                ("blood_loss_risk", "Risk of >500ml blood loss (7ml/kg in children)? If yes, adequate IV access/fluids planned"),
            };

            public static readonly IReadOnlyList<(string Key, string Label)> TimeOut = new[]
            {
                ("team_introduced", "All team members introduced by name and role"),
                ("verbal_confirmation", "Surgeon/anaesthetist/nurse verbally confirm patient, site, procedure"),
                ("critical_events_surgeon", "Anticipated critical events reviewed — surgeon"),
                ("critical_events_anaesthetist", "Anticipated critical events reviewed — anaesthetist"),
                ("critical_events_nursing", "Anticipated critical events reviewed — nursing team"),
                ("antibiotic_prophylaxis", "Antibiotic prophylaxis given within last 60 minutes? / not applicable"),
                ("imaging_displayed", "Essential imaging displayed? / not applicable"),
            };

            public static readonly IReadOnlyList<(string Key, string Label)> SignOut = new[]
            {
                ("procedure_name_recorded", "Nurse verbally confirms: name of procedure recorded"),
                ("counts_correct", "Instrument, sponge, and needle counts correct / not applicable"),
                ("specimen_labeled", "Specimen labeled correctly, including patient name"),
                ("equipment_problems", "Equipment problems to be addressed identified"),
                ("recovery_concerns_reviewed", "Surgeon/anaesthetist/nurse review key concerns for recovery and management"),
            };
        }

        // ---- CSSD ----

        public static class InstrumentSetStatus
        {
            public const string Available = "AVAILABLE";
            public const string Issued = "ISSUED";
            public const string InUse = "IN_USE";
            public const string ReturnedSoiled = "RETURNED_SOILED";
            public const string Washing = "WASHING";
            public const string Packed = "PACKED";
            public const string Sterilizing = "STERILIZING";
            public const string Sterile = "STERILE";
            public const string Quarantined = "QUARANTINED";
            public const string Retired = "RETIRED";
        }

        public static class SterilizationCycleType
        {
            public const string Steam = "STEAM";
            public const string Eto = "ETO";
            public const string Plasma = "PLASMA";
            public static readonly string[] All = { Steam, Eto, Plasma };
        }

        public static class IndicatorResult
        {
            public const string Pass = "PASS";
            public const string Fail = "FAIL";
            public const string Pending = "PENDING";   // biological indicator only
        }

        public static class InstrumentSetMovementType
        {
            public const string IssueToOt = "ISSUE_TO_OT";
            public const string Return = "RETURN";
            public const string SendToWash = "SEND_TO_WASH";
            public const string Pack = "PACK";
            public const string Quarantine = "QUARANTINE";
            public const string Discard = "DISCARD";
            public const string ReceiveSterile = "RECEIVE_STERILE";
            public static readonly string[] All = { IssueToOt, Return, SendToWash, Pack, Quarantine, Discard, ReceiveSterile };
        }

        // ---- ICU ----

        /// <summary>Level 1 = HDU/step-down critical-care input; Level 2 = single organ support; Level 3 = multi-organ support.</summary>
        public static class IcuLevelOfCare
        {
            public const string Level1 = "LEVEL_1";
            public const string Level2 = "LEVEL_2";
            public const string Level3 = "LEVEL_3";
            public static readonly string[] All = { Level1, Level2, Level3 };
        }

        public static class ApacheChronicHealthCategory
        {
            public const string None = "NONE";
            public const string ElectivePostOp = "ELECTIVE_POSTOP";
            public const string NonoperativeOrEmergencyPostOp = "NONOPERATIVE_OR_EMERGENCY_POSTOP";
            public static readonly string[] All = { None, ElectivePostOp, NonoperativeOrEmergencyPostOp };
        }

        /// <summary>SOFA cardiovascular component — standard categorical tiers (as charted on paper), not a raw infusion-rate calculator.</summary>
        public static class SofaVasopressorTier
        {
            public const string None = "NONE";
            public const string MapLow = "MAP_LOW";
            public const string DopamineLowOrDobutamine = "DOPAMINE_LOW_OR_DOBUTAMINE";
            public const string DopamineMedOrEpiLowOrNorepiLow = "DOPAMINE_MED_OR_EPI_LOW_OR_NOREPI_LOW";
            public const string DopamineHighOrEpiHighOrNorepiHigh = "DOPAMINE_HIGH_OR_EPI_HIGH_OR_NOREPI_HIGH";
            public static readonly string[] All = { None, MapLow, DopamineLowOrDobutamine, DopamineMedOrEpiLowOrNorepiLow, DopamineHighOrEpiHighOrNorepiHigh };
        }

        // ---- Billing / GST ----

        /// <summary>Soft-validated — BedMaster.WardType is free-text with no DB CHECK constraint (pre-existing
        /// data may not match), same posture as ConsentTypeCode. IcuFamily drives the GST resolver's
        /// "always exempt regardless of tariff" rule.</summary>
        public static class WardType
        {
            public const string General = "GENERAL";
            public const string Icu = "ICU";
            public const string Nicu = "NICU";
            public const string Picu = "PICU";
            public const string Hdu = "HDU";
            public const string Ccu = "CCU";
            public const string Iccu = "ICCU";
            public const string Private = "PRIVATE";
            public const string SemiPrivate = "SEMI_PRIVATE";
            public const string Other = "OTHER";

            public static readonly string[] All = { General, Icu, Nicu, Picu, Hdu, Ccu, Iccu, Private, SemiPrivate, Other };
            public static readonly string[] IcuFamily = { Icu, Nicu, Picu, Hdu, Ccu, Iccu };

            public static bool IsIcuFamily(string? wardType) =>
                !string.IsNullOrWhiteSpace(wardType) && IcuFamily.Contains(wardType.Trim().ToUpperInvariant());
        }
    }
}
