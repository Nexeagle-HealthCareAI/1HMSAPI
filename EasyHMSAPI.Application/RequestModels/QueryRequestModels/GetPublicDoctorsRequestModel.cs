using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Platform-wide — returns doctors across every publicly-listed hospital, not one
    // hospital scoped by an API key. City/State/SpecialtyCategory/Search are all
    // optional narrowing filters, pushed into the SQL query itself rather than
    // filtered in-memory after the fact — needed now that the platform-wide doctor
    // count has grown past what "return everyone, every time" can serve efficiently.
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorsRequestModel : IRequest<GetPublicDoctorsResponseModel>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public string? City { get; set; }
        public string? State { get; set; }
        // Matches dbo.MedicalSpecialities.PatientFacingCategory verbatim (e.g. "Neurologist"),
        // not the NexEagleWebsite specialtyId slug — the caller (Next.js proxy route) is
        // responsible for that translation, same split of responsibility already used
        // between NexEagleWebsite's specialtyId taxonomy and this DB's NMC categories.
        public string? SpecialtyCategory { get; set; }
        public string? Search { get; set; }

        // When set, scopes the listing to one hospital (e.g. the OPD QR flow, after resolving a
        // scanned code to a HospitalId) instead of the whole platform-wide directory. A hospitalId-
        // scoped query bypasses Hospital.IsPubliclyListed (a marketplace opt-in flag unrelated to a
        // hospital's own front-desk QR usage) -- it still requires the hospital be IsActive and not
        // archived. Doctor.IsPubliclyListed/IsDelistedByAdmin are unchanged either way: an
        // individual doctor's own opt-out is respected regardless of how they're being looked up.
        public Guid? HospitalId { get; set; }

        // When set, narrows to exactly one doctor -- e.g. PublicController's single-doctor
        // lookup (GET public/doctors/{doctorId}, used by the WhatsApp bot's deterministic
        // DRBOOK trigger and by the QR-code endpoint). Still requires the SAME
        // IsPubliclyListed/IsActive/not-archived/not-delisted gates as the platform-wide
        // listing -- a doctor's own profile page can only exist for a doctor that already
        // passes those, so there is nothing to bypass here (unlike HospitalId above).
        public Guid? DoctorId { get; set; }
    }
}
