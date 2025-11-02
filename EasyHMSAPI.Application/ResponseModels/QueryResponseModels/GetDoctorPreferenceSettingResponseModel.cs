using EasyHMSAPI.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDoctorPreferenceSettingResponseModel
    {
        public DoctorSectionPreference? Preference { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}