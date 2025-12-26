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
        public string? MedicineName { get; set; }
        public string? BrandName { get; set; }
        public string? GenericName { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? UsageDescription { get; set; }
        public string? SideEffects { get; set; }
        public int? Price { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public long? UsageCount { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public string? LastModifiedBy { get; set; }
    }
}
