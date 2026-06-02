using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>Linked-record counts for one UHID — powers the "what will transfer" merge preview.</summary>
    [ExcludeFromCodeCoverage]
    public class GetPatientRecordCountsRequestModel : IRequest<GetPatientRecordCountsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
    }
}
