using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorReviewsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicReviewItem> Reviews { get; set; } = new();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicReviewItem
    {
        public Guid ReviewId { get; set; }
        public string? AuthorName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = null!;
        public int HelpfulCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
