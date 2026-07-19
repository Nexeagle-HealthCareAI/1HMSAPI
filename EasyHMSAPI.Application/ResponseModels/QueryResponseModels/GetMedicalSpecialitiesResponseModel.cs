using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetMedicalSpecialitiesResponseModel
    {
        public List<MedicalSpecialityItem> Items { get; set; } = new List<MedicalSpecialityItem>();
    }

    [ExcludeFromCodeCoverage]
    public class MedicalSpecialityItem
    {
        public Guid SpecialityId { get; set; }
        public string QualificationTypeCode { get; set; } = null!;   // 'MD' | 'MS' | 'DM' | 'MCh'
        public string QualificationTypeName { get; set; } = null!;   // 'Doctor of Medicine', etc.
        public string Name { get; set; } = null!;                    // NMC name, e.g. 'Cardiology'
        public string? PatientFacingName { get; set; }
        public string? PatientFacingCategory { get; set; }
        public int SortOrder { get; set; }
    }
}
