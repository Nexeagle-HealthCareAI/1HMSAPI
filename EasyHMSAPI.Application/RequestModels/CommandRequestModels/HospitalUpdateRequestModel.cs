using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModel
{
    [ExcludeFromCodeCoverage]
    public class HospitalUpdateRequestModel : MediatR.IRequest<HospitalUpdateResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Email { get; set; }
        public string? Contact { get; set; }
        public string? AlternateContact { get; set; }
        public string? Website { get; set; }
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? TimeZone { get; set; }
        public string? GstIn { get; set; }
        public string? PanNumber { get; set; }
        public string? NabhNabl { get; set; }
        // Nullable bool, not the string-empty-means-unset sentinel every other field here
        // uses — the handler checks .HasValue explicitly before applying it.
        public bool? IsPubliclyListed { get; set; }
        // GPS pin for the public doctor directory's "get directions" link. Same .HasValue-guarded
        // convention as IsPubliclyListed above.
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
} 