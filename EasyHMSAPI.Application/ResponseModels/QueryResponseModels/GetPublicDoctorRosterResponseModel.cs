using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorRosterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicDoctorRosterItem> Doctors { get; set; } = new();
    }

    // Deliberately narrower than PublicDoctorInfo AND HospitalDoctorItem -- no fee/bio/photo/
    // rating/contact/IsPubliclyListed. This exists only so an LLM can phonetically match a
    // mis-transcribed name against a real one before calling find_doctors; nothing here is
    // ever spoken or shown to a patient directly.
    [ExcludeFromCodeCoverage]
    public class PublicDoctorRosterItem
    {
        public Guid DoctorId { get; set; }
        public string? FullName { get; set; }
        public string? DepartmentName { get; set; }
        // Matches dbo.MedicalSpecialities.PatientFacingCategory verbatim -- same field name/
        // semantics as GetPublicDoctorsRequestModel.SpecialtyCategory's filter.
        public string? SpecialtyCategory { get; set; }
    }
}
