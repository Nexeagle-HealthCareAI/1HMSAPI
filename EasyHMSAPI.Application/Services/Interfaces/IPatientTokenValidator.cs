namespace EasyHMSAPI.Application.Services.Interfaces
{
    public record PatientTokenValidationResult(bool IsValid, string? Mobile, string? Reason);

    // Validates a patient-scoped JWT (issued by PatientOtpVerifyHandler) WITHOUT going through the
    // app's standard [Authorize]/JWT-bearer pipeline — that pipeline is configured for hospital
    // STAFF tokens and populates HttpContext.User globally for every filter in the app (including
    // HospitalAccessFilter). A patient token must never be able to satisfy [Authorize] on a
    // staff-only endpoint, so it's checked here, endpoint-locally, instead.
    public interface IPatientTokenValidator
    {
        Task<PatientTokenValidationResult> ValidateAsync(string? authorizationHeader, CancellationToken cancellationToken);
    }
}
