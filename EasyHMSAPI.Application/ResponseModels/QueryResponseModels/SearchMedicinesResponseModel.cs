using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SearchMedicinesResponseModel
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public List<PersonalMedicineDataModel>? PersonalMedicine { get; set; }
        public List<MasterMedicineDataModel>? MasterMedicine { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PersonalMedicineDataModel
    {
        public string? MedicineName { get; set; }
        public string? GenericName { get; set; }
        public string? BrandName { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? UsageDescription { get; set; }
        public string? SideEffects { get; set; }
        public int? Price { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MasterMedicineDataModel
    {
        public int MedicineId { get; set; }
        public string? MedicineName { get; set; }
        public string? GenericName { get; set; }
        public string? BrandName { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? UsageDescription { get; set; }
        public string? SideEffects { get; set; }
        public Decimal? Price { get; set; }
    }
}
