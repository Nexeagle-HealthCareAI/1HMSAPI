using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetChainDoctorsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public Guid? ChainId { get; set; }
        public List<ChainDoctorItem> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ChainDoctorItem
    {
        public Guid DoctorId { get; set; }
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        // Hospitals (within the chain) this doctor currently works at.
        public List<ChainDoctorHospital> Hospitals { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ChainDoctorHospital
    {
        public Guid HospitalId { get; set; }
        public string Name { get; set; } = null!;
    }
}
