using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordApacheIIScoreResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ApacheIIScoreId { get; set; }
        public int? TotalScore { get; set; }
    }
}
