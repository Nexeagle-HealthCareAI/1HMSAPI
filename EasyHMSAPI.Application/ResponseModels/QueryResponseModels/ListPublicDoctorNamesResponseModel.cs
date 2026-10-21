using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ListPublicDoctorNamesResponseModel
    {
        public bool Success { get; set; }
        public List<PublicDoctorName> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PublicDoctorName
    {
        public Guid DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? City { get; set; }
    }
}
