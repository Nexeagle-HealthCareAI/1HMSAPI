using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordSofaScoreResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? SofaScoreId { get; set; }
        public int? TotalScore { get; set; }
    }
}
