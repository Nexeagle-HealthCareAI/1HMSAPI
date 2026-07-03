namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IWhatsAppMessagingService
    {
        Task<bool> SendOtpAsync(string mobileNumber, string otp);
        Task<bool> SendInvitationAsync(string mobileNumber, string hospitalName, string role, string registrationUrl);
        Task<bool> SendLoginDetailsAsync(string mobileNumber, string hospitalName, string loginId, string password);
        Task<bool> SendAppointmentConfirmationAsync(string mobileNumber, string patientName, string hospitalName, string doctorName, string tokenNumber, string appointmentDate, string appointmentTime);
        Task<bool> SendPrescriptionAsync(string mobileNumber, string documentLink, string fileName, string hospitalName, string doctorName);

        /// <summary>Plain-text discharge notice (patient name, hospital, discharge date) — no
        /// document link this phase. Requires a "discharge_notice_eng" template approved in Meta
        /// Business Manager; returns false (no-op, matches SendLoginDetailsAsync's behavior today)
        /// until that template exists.</summary>
        Task<bool> SendDischargeNotificationAsync(string mobileNumber, string patientName, string hospitalName, string dischargeDate);
    }
}