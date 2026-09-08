using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicLabsResponseModel
    {
        public bool Success { get; set; }
        public List<PublicLabInfo> Labs { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    // Name/Address/RegistrationNumber already reflect LabConfiguration's own override-or-fall-back-
    // to-hospital resolution (see GetPublicLabsHandler) -- the public consumer never needs to know
    // whether a value came from the lab's own override or the hospital's generic profile.
    [ExcludeFromCodeCoverage]
    public class PublicLabInfo
    {
        public Guid LabId { get; set; }
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public List<string> TestCategories { get; set; } = new();
    }
}
