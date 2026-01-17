namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IWhatsAppMessagingService
    {
        Task<bool> SendOtpAsync(string mobileNumber, string otp);
        Task<bool> SendInvitationAsync(string mobileNumber, string hospitalName, string role, string registrationUrl);
        Task<bool> SendAppointmentConfirmationAsync(string mobileNumber, string patientName, string hospitalName, string doctorName, string tokenNumber, string appointmentDate, string appointmentTime);
        Task<bool> SendPrescriptionAsync(string mobileNumber, string documentLink, string fileName, string hospitalName, string doctorName);
    }
}