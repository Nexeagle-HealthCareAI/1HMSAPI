using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionSettingsResponseModel
    {
        public Guid? PrescriptionSettingsId { get; set; }
        public Guid? DoctorId { get; set; }
        public PrescriptionSettingsDataModel? Settings { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
