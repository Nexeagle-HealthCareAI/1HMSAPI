using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>
    /// Returning-patient detail for the IPD admission screen: full demographics (for one-click
    /// re-admit pre-fill) plus the patient's admission history with discharge-summary previews.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class GetPatientAdmissionsRequestModel : IRequest<GetPatientAdmissionsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
    }
}
