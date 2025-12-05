using System.Diagnostics.CodeAnalysis;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdatePrescriptionSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid PrescriptionSettingId { get; set; }
    }
}
