namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class HospitalKpiMatrixResponseModel
    {
        public Guid HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<StatusKpi> StatusKpis { get; set; } = new List<StatusKpi>();
    }

    public class StatusKpi
    {
        public string StatusCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int PatientCount { get; set; }
    }
}
