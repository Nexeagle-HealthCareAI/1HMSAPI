using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientVisitSummaryPdfResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PdfUrl { get; set; }
    }
}
