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
