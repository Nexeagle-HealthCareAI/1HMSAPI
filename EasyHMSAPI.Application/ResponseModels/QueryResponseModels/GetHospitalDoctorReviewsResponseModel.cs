using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalDoctorReviewsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AdminReviewItem> Reviews { get; set; } = new();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    // Unlike PublicReviewItem, includes hidden reviews and the moderation flag itself, plus
    // SubmittedIp for abuse triage — never exposed on the public endpoint.
    [ExcludeFromCodeCoverage]
    public class AdminReviewItem
    {
        public Guid ReviewId { get; set; }
        public string? AuthorName { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public int HelpfulCount { get; set; }
        public bool IsHidden { get; set; }
        public bool IsHospitalResponse { get; set; }
        public string? SubmittedIp { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
