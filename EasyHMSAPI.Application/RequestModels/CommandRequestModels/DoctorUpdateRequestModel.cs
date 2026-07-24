using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorUpdateRequestModel : MediatR.IRequest<DoctorUpdateResponseModel>
    {
        public Guid UserId { get; set; }
        public Guid HospitalDepartmentMappingId { get; set; }
        public string? LicenseNumber { get; set; }
        public List<string>? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Bio { get; set; }
        public string? PrimaryDepartment { get; set; }
        public string? Department { get; set; }
        public List<string>? Specializations { get; set; }
        // Optional link into the NMC qualification-ladder catalog (dbo.MedicalSpecialities) —
        // additive, sits alongside Qualification/Department/Specializations above.
        public Guid? PrimaryMedicalSpecialityId { get; set; }
        public List<string>? Languages { get; set; }
        public string? PublicContactEmail { get; set; }
        public string? PublicContactPhone { get; set; }
        // Set only when an admin is editing a doctor other than themselves, from the Public
        // Directory tile editor. Triggers HospitalAccessFilter's automatic caller-is-a-member-of-
        // this-hospital check (it detects any bound "HospitalId" property), plus an explicit
        // doctor-belongs-to-this-hospital check in the handler — same two-layer guard
        // UpdateDoctorPublicListingHandler already uses. Left null, self-service edits behave
        // exactly as before.
        public Guid? HospitalId { get; set; }
        // Only the Public Directory tile editor manages the OPD discount — the doctor's own
        // self-service profile form (same endpoint, HospitalId omitted) never renders this
        // section. When false, the three fields below are ignored entirely so that save path can
        // never silently wipe a discount it doesn't show. When true, applied as a full replace
        // (DiscountPercent = null clears it) — mirrors CMSAPI's
        // BulkUpdateDoctorMarketingRequest.UpdateDiscount flag. This discount is Doctor
        // Dekho/online-booking-only: it has no effect on easyHMSWeb's own in-hospital appointment
        // or billing flows.
        public bool UpdateDiscount { get; set; } = false;
        public decimal? DiscountPercent { get; set; }
        public DateTime? DiscountStartAt { get; set; }
        public DateTime? DiscountEndAt { get; set; }
    }
}
