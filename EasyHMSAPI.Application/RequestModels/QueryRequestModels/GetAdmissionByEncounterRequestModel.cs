using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionByEncounterRequestModel : IRequest<GetAdmissionByEncounterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid EncounterId { get; set; }
    }
}
