using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PatientFamilyMemberItem
    {
        public Guid MemberId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Relation { get; set; } = string.Empty;
        public int? Age { get; set; }
        public string? AgeUnit { get; set; }
        public string? Sex { get; set; }
        public string? BloodGroup { get; set; }
        public string? Allergies { get; set; }
        // How many records are kept for this person in the Health Locker.
        public int DocumentCount { get; set; }
    }

    // List, add, change and remove all answer in this one shape. Member is set after an add or change.
    [ExcludeFromCodeCoverage]
    public class PatientFamilyMembersResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; } = 200;
        public int Max { get; set; } = EasyHMSAPI.Application.Services.PatientProfileRules.MaxFamilyMembers;
        public List<PatientFamilyMemberItem> Members { get; set; } = new();
        public PatientFamilyMemberItem? Member { get; set; }
    }
}
