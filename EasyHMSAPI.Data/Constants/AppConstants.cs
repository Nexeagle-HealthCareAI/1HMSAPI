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

        public static readonly string TokenStrategy_Sequential = "SEQUENTIAL";
        
        public static readonly string Prescription_ActionType_Submit = "submit";
        public static readonly string Prescription_ActionType_Draft = "draft";

        public static readonly string LookupType_Procedure = "PROCEDURE";
        public static readonly string LookupType_Investigation = "INVESTIGATION";
    }
}
