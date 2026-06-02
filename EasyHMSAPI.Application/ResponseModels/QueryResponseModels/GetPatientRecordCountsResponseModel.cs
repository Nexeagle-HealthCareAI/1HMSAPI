using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientRecordCountsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public string? PatientId { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public bool IsMerged { get; set; }
        public string? MergedIntoPatientId { get; set; }

        public int Admissions { get; set; }
        public int Appointments { get; set; }
        public int Invoices { get; set; }
        public int Payments { get; set; }
        public int Prescriptions { get; set; }
        public int Encounters { get; set; }
        public int Alerts { get; set; }
        public int Total { get; set; }
    }
}
