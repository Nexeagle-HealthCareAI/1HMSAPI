using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Data.Constants
{
    [ExcludeFromCodeCoverage]
    public static class AppConstants
    {
        public static readonly string[] AllowedShiftNames = { "afternoon", "evening", "morning" };

        public static readonly string ShiftDataSource_Default = "Default";
        public static readonly string ShiftDataSource_Override = "Override";
        public static readonly string ShiftDataSource_TimeOff = "TimeOff";

        public static readonly string AppointmentStatus_Cancelled = "CANCELLED";
        public static readonly string AppointmentStatus_VitalsRequired = "VITALS_REQUIRED";
        public static readonly string AppointmentStatus_Future = "FUTURE";
        public static readonly string AppointmentStatus_Ready = "READY";
        public static readonly string AppointmentStatus_Completed = "COMPLETED";
        public static readonly string AppointmentStatus_LabRequired = "LAB_REQUIRED";
        public static readonly string AppointmentStatus_AwaitingReconsult = "AWAITING_RECONSULT";
        public static readonly string AppointmentStatus_UnderConsult = "UNDER_CONSULT";
        public static readonly string AppointmentStatus_PreAppointment = "PRE_APPOINTMENT";

        public static readonly string BookingSource_Internal = "INTERNAL";
        public static readonly string BookingSource_NexeaglePublic = "NEXEAGLE_PUBLIC";

        // AnalyticsEvent.EventType values — the CMS Insights tab's Auth Funnel / Booking Funnel /
        // All Searches reports group by these exact strings.
        public static readonly string AnalyticsEventType_LoginInitiated = "login_initiated";
        public static readonly string AnalyticsEventType_OtpSent = "otp_sent";
        public static readonly string AnalyticsEventType_OtpVerified = "otp_verified";
        public static readonly string AnalyticsEventType_OtpVerifyFailed = "otp_verify_failed";
        public static readonly string AnalyticsEventType_SearchPerformed = "search_performed";
        public static readonly string AnalyticsEventType_DoctorProfileViewed = "doctor_profile_viewed";
        public static readonly string AnalyticsEventType_BookingStepReached = "booking_step_reached";

        // HospitalLead.Source / HospitalLead.LeadType values -- the Lead Generation page's own
        // vocabulary, deliberately separate from AnalyticsEventType_* above (see HospitalLead.cs).
        public static readonly string LeadSource_DoctorDekho = "DoctorDekho";
        public static readonly string LeadSource_WhatsApp = "WhatsApp";
        public static readonly string LeadType_DoctorNameSearch = "DoctorNameSearch";
        public static readonly string LeadType_HospitalNameSearch = "HospitalNameSearch";
        public static readonly string LeadType_DoctorProfileView = "DoctorProfileView";
        public static readonly string LeadType_HospitalPageView = "HospitalPageView";

        public static readonly string TokenStrategy_Sequential = "SEQUENTIAL";

        // OPD queue check-in (QR flow) -- AppointmentToken.Status values.
        public static readonly string QueueTokenStatus_Waiting = "WAITING";
        public static readonly string QueueTokenStatus_Called = "CALLED";
        public static readonly string QueueTokenStatus_Done = "DONE";
        public static readonly string QueueTokenStatus_NoShow = "NOSHOW";

        public static readonly string QueueArrivalMethod_Geofence = "Geofence";
        public static readonly string QueueArrivalMethod_StaffOverride = "StaffOverride";

        // How close (in meters) a patient's reported GPS position must be to Hospital.Latitude/
        // Longitude for POST public/tokens to accept a self-check-in without staff involvement.
        public const double GeofenceRadiusMeters = 200;

        // A skip is capped at this many times per token before it requires manual reception
        // handling (see QueueController.Skip) -- the spec doesn't define what happens past the
        // cap, so this is a hard stop rather than a guessed auto-action.
        public const int QueueMaxSkipsPerToken = 2;

        // How many positions later a skipped patient's QueueSequence moves.
        public const int QueueSkipRequeueOffset = 3;

        // Rough per-patient consult duration used to estimate a queued patient's wait time —
        // documented as an estimate, not a promise.
        public const int QueueAverageConsultMinutes = 10;
        
        public static readonly string Prescription_ActionType_Submit = "submit";
        public static readonly string Prescription_ActionType_Draft = "draft";

        public static readonly string LookupType_Procedure = "PROCEDURE";
        public static readonly string LookupType_Investigation = "INVESTIGATION";

        public static readonly string AppointmentType_OldFee = "Old/Fee";
        public static readonly string AppointmentType_OldNoFee = "Old/No-Fee";
        public static readonly string AppointmentType_New = "New";

        public static readonly string PatientSex_Male = "Male";
        public static readonly string PatientSex_Female = "Female";

        public static readonly string VisitType_OPD = "OPD";
        public static readonly string VisitType_LAB = "LAB";
        public static readonly string VisitType_PHARMACY = "PHARMACY";
        public static readonly string VisitType_IPD = "IPD";
        public static readonly string VisitType_ER = "ER";
        public static readonly string VisitType_OTHER = "OTHER";
    }
}
