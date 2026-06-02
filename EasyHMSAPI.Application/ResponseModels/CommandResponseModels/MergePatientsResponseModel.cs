using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class MergePatientsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public string? CanonicalPatientId { get; set; }
        // Records moved per table (table name -> rows repointed).
        public Dictionary<string, int> MovedCounts { get; set; } = new();
        public int TotalMoved { get; set; }
    }
}
