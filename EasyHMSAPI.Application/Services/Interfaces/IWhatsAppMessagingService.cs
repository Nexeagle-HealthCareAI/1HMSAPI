using EasyHMSAPI.Application.Services.Models;
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

        /// <summary>Sends the discharge summary PDF as a WhatsApp document attachment — same
        /// document-header template shape as SendPrescriptionAsync. Requires a
        /// "discharge_summary_sent" template approved in Meta Business Manager; returns false
        /// (no-op) until that template exists.</summary>
        Task<bool> SendDischargeSummaryAsync(string mobileNumber, string documentLink, string fileName, string hospitalName, string doctorName);

        /// <summary>Sends a plain text summary of the payslip. Requires a "payslip_generated_eng"
        /// template approved in Meta Business Manager; returns false until that template exists.</summary>
        Task<bool> SendPayslipNotificationAsync(string mobileNumber, string employeeName, string monthYear, decimal netSalary, string hospitalName);

        /// <summary>Sends the approved pathology report PDF as a WhatsApp document attachment --
        /// same document-header template shape as SendPrescriptionAsync/SendDischargeSummaryAsync.
        /// Requires a "lab_report_sent" template approved in Meta Business Manager; returns false
        /// (no-op) until that template exists.</summary>
        Task<bool> SendLabReportAsync(string mobileNumber, string documentLink, string fileName, string hospitalName, string patientName);

        /// <summary>Notifies a doctor of a new online (Doctor Dekho) appointment request — patient
        /// name, address, and a masked contact number (never the full number, over WhatsApp), plus
        /// a link to log into 1HMS to view it. Requires a "doctor_new_online_appointment" template
        /// approved in Meta Business Manager; returns false (no-op, matches SendLoginDetailsAsync's
        /// behavior today) until that template exists.</summary>
        Task<bool> SendDoctorNewOnlineAppointmentAlertAsync(string mobileNumber, string doctorName, string patientName, string maskedPatientMobile, string patientAddress, string loginUrl);

        /// <summary>Invitation link for a Health Wiki contributor (review or write an article, or join).
        /// Requires a "health_wiki_invite" template (language en, named body variables contributor_name, task, url)
        /// approved in Meta Business Manager; returns false (no-op) until it exists or while WhatsApp:IsEnabled is off,
        /// so the CMS shows the link to copy instead.</summary>
        Task<bool> SendHealthWikiInviteAsync(string mobileNumber, string contributorName, string task, string url);

        /// <summary>Sends one of the Health Wiki's approved templates (see HealthWikiConstants.Template). Parameters are the template's NAMED body
        /// variables, matched by name in Meta, so each template's names must be exact. A value is flattened to one line and cut to a safe
        /// length. Returns false (no-op) while WhatsApp:IsEnabled is off or when Meta refuses (for example a template not approved yet).</summary>
        Task<bool> SendHealthWikiTemplateAsync(string mobileNumber, string templateName, IReadOnlyList<KeyValuePair<string, string>> parameters);

        /// <summary>Same send as <see cref="SendHealthWikiTemplateAsync"/>, but also returns the message id WhatsApp gave it, so a later delivery
        /// status (delivered, read, failed) can be matched to the message.</summary>
        Task<WhatsAppSendResult> SendHealthWikiTemplateTrackedAsync(string mobileNumber, string templateName, IReadOnlyList<KeyValuePair<string, string>> parameters);

        /// <summary>Any approved template with NAMED body variables (same send as the Health Wiki ones), returning WhatsApp's message id.
        /// Used by the emergency takeover ("emergency_nearby_alert", "emergency_patient_number"). Returns Refused while WhatsApp:IsEnabled
        /// is off or until Meta approves the template.</summary>
        Task<WhatsAppSendResult> SendNamedTemplateTrackedAsync(string mobileNumber, string templateName, IReadOnlyList<KeyValuePair<string, string>> parameters);
    }
}