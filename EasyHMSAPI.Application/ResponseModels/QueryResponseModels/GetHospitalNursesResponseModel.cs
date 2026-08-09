using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalNursesResponseModel
    {
        public List<HospitalNurseItem> Nurses { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HospitalNurseItem
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string MobileNumber { get; set; } = null!;
    }
}
