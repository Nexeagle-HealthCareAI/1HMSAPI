using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class AdmitPatientRequestModel : IRequest<AdmitPatientResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public Guid? PrimaryDoctorId { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public DateTime? ExpectedDischargeAt { get; set; }
        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }
        public string? LoggedInUserName { get; set; }
    }
}
