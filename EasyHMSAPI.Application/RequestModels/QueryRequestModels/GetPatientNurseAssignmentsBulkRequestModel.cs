using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Same result shape as GetPatientNurseAssignmentsRequestModel (PatientNurseAssignmentItem
    // already carries its own AdmissionId), just fetched for many admissions in one round trip --
    // lets the ward board load every bed's assignments with a single call instead of one GET per
    // occupied bed.
    [ExcludeFromCodeCoverage]
    public class GetPatientNurseAssignmentsBulkRequestModel : IRequest<GetPatientNurseAssignmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public List<Guid> AdmissionIds { get; set; } = new();
        public bool ActiveOnly { get; set; } = true;
    }
}
