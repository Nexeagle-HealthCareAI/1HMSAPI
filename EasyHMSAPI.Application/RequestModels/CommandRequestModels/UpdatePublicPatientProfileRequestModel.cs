using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // The signed-in patient saves their own details on Doctor Dekho. Mobile comes from the verified patient token, never from the client.
    [ExcludeFromCodeCoverage]
    public class UpdatePublicPatientProfileRequestModel : IRequest<GetPublicPatientProfileResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        // Send FirstName (+ LastName); FullName is still accepted from older callers and split on its first space.
        public string? FullName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        // A+ | A- | B+ | B- | AB+ | AB- | O+ | O-
        public string? BloodGroup { get; set; }
        public int? Age { get; set; }
        // Y | M | D
        public string? AgeUnit { get; set; }
        // Male | Female | Other
        public string? Sex { get; set; }
        public string? Email { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianRelation { get; set; }
    }
}
