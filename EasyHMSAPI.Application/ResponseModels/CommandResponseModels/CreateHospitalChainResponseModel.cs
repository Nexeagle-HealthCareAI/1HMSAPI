using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateHospitalChainResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public Guid? ChainId { get; set; }
        public int HospitalsLinked { get; set; }
    }
}
