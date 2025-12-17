using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPreferredMedicinesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PreferredMedicineDataModel>? PreferredMedicines { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PreferredMedicineDataModel
    {
        public long PrefferedId { get; set; }
        public string? GenericName { get; set; }
        public string? BrandName { get; set; }
        public string? Form { get; set; }
        public string? StrengthValue { get; set; }
        public string? StrengthUnit { get; set; }
        public string? Route { get; set; }
        public string? Dose { get; set; }
        public string? Indication { get; set; }
        public string? Notes { get; set; }
        public string? MedicineId { get; set; }
        public int? UsageCount { get; set; }
        public DateTime? LastModifiedAt { get; set; }
    }
}
