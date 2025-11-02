using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalDetailsRequestModel : MediatR.IRequest<GetHospitalDetailsResponseModel?>
    {
        public Guid HospitalId { get; }

        public GetHospitalDetailsRequestModel(Guid hospitalId)
        {
            HospitalId = hospitalId;
        }
    }
} 