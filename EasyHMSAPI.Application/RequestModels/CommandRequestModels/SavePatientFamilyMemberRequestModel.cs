using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Adds a family member (MemberId empty) or changes one (MemberId set, taken from the route). Mobile is the verified patient token's.
    [ExcludeFromCodeCoverage]
    public class SavePatientFamilyMemberRequestModel : IRequest<PatientFamilyMembersResponseModel>
    {
        [JsonIgnore]
        public string Mobile { get; set; } = string.Empty;
        [JsonIgnore]
        public Guid? MemberId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Relation { get; set; }
        public int? Age { get; set; }
        // Y | M | D
        public string? AgeUnit { get; set; }
        public string? Sex { get; set; }
        public string? BloodGroup { get; set; }
        public string? Allergies { get; set; }
    }
}
