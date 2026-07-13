using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Reassigns the admission's admitting/primary doctor: releases the current ACTIVE
    // AdmissionDoctorAssignment row (stamps UnassignedAt/By) and inserts a new ACTIVE one,
    // atomically -- same transactional shape as TransferBedRequestModel. Also updates
    // Admission.PrimaryDoctorId (the live field every billing/referral consumer reads).
    [ExcludeFromCodeCoverage]
    public class ChangeAdmittingDoctorRequestModel : IRequest<ChangeAdmittingDoctorResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid DoctorId { get; set; }
        public string? Notes { get; set; }
    }
}
