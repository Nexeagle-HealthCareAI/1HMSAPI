using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPreferredMedicineRequestModel : IRequest<UpsertPreferredMedicineResponseModel>
    {
        public Guid DoctorId { get; set; }
        public PreferredMedicineDataModel Medicine { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class PreferredMedicineDataModel
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
