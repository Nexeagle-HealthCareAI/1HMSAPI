using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AddDoctorToHospitalResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public bool AlreadyMember { get; set; }
    }
}
