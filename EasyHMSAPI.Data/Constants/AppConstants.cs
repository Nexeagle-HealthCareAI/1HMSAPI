namespace EasyHMSAPI.Data.Constants
{
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

        public static readonly string TokenStrategy_Sequential = "SEQUENTIAL";

        public static readonly string AssetType_HeaderImage = "header_image";
        public static readonly string AssetType_FooterImage = "footer_image";
        public static readonly string AssetType_SignatureImage = "signature_image";

        public static readonly List<string> LookupTypes =
        [
            "CHIEF_COMPLAINT",
            "HISTORY",
            "COMORBIDITY",
            "EXAMINATION",
            "DIAGNOSIS",
            "DIFFERENTIAL_DIAGNOSIS",
            "INVESTIGATION",
            "PROCEDURE",
            "MEDICATION",
            "ADVICE",
            "NONPHARM_ADVICE",
            "CERTIFICATE",
            "NOTE",
            "IMMUNIZATION",
            "FOLLOW_UP",
            "ATTACHMENT"
        ];



    }
}
