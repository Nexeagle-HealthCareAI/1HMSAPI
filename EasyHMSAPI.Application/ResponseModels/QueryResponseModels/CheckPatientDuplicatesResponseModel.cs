using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CheckPatientDuplicatesResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public List<DuplicateMatch> Matches { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DuplicateMatch
    {
        public string PatientId { get; set; } = null!;
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? AgeYears { get; set; }
        public string? Sex { get; set; }
        public string? City { get; set; }
        // 0..1 name similarity (Jaro-Winkler).
        public double Similarity { get; set; }
        // NEAR_CERTAIN / PROBABLE / POSSIBLE
        public string Confidence { get; set; } = null!;
        // Which signals matched: NAME, MOBILE, DOB, AADHAAR4
        public List<string> MatchedOn { get; set; } = new();
    }
}
