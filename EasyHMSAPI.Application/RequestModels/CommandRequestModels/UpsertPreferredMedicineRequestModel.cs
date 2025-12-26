using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPreferredMedicineRequestModel : IRequest<UpsertPreferredMedicineResponseModel>
    {
        public long? PreferrredId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
        public PreferredMedicineModel Medicine { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class PreferredMedicineModel
    {
        public string? MedicineName { get; set; }
        public string? BrancdName { get; set; }
        public string? Manufacturer { get; set; }
        public string? GenericName { get; set; }
        public string? BrandName { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public int? Price { get; set; }
        public string? UsageDescription { get; set; }
        public string? SideEffects { get; set; }
        public string? Notes { get; set; }
        public int? UsageCount { get; set; }
    }
}
