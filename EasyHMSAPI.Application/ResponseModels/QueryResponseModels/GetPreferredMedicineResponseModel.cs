using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPreferredMedicineResponseModel
    {
        public long PrefferedId { get; set; }
        public string GenericName { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string Form { get; set; } = string.Empty;
        public string StrengthValue { get; set; } = string.Empty;
        public string StrengthUnit { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Dose { get; set; } = string.Empty;
        public string Indication { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string MedicineId { get; set; } = string.Empty;
    }
}
