using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetVendorsResponseModel
    {
        public List<VendorDataModel> Vendors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class VendorDataModel
    {
        public Guid VendorId { get; set; }
        public string VendorCode { get; set; } = null!;
        public string VendorName { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? GstNumber { get; set; }
        public string? DrugLicenseNumber { get; set; }
        public int PaymentTermsDays { get; set; }
        public bool IsActive { get; set; }
    }
}
