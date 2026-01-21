using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientsByHospitalIdResponseModel
    {
        public Guid HospitalId { get; set; }
        public List<DoctorDataModel>? DoctorsData { get; set; }
        public List<PatientDataModel>? PatientsData { get; set; }
        public HospitalPatientStatisticsModel? Statistics { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class HospitalPatientStatisticsModel
    {
        public int TotalPatientCount { get; set; }
        public int MalePatientCount { get; set; }
        public int FemalePatientCount { get; set; }
        public NewPatientRegistrationModel? NewRegistrations { get; set; }
    }

    public class NewPatientRegistrationModel
    {
        public int Today { get; set; }
        public int Yesterday { get; set; }
        public int ThisWeek { get; set; }
        public int ThisMonth { get; set; }
        public int ThisYear { get; set; }
        public int PreviousYear { get; set; }
    }

    public class DoctorDataModel
    {
        public string? DoctorName { get; set; }
        public int? TotalPatientCount { get; set; }
        public int? FemalePatientCount { get; set; }
        public int? MalePatientCount { get; set; }
        public int? SharedPatientCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientDataModel
    {
        public string? PatientId { get; set; }
        public string? Name { get; set; }
        public int? Age { get; set; }
        public string? Sex { get; set; }
        public string? Contact { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PinCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? DoctorNames { get; set; }
    }
}
