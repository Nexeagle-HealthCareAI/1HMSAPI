using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHospitalDetailsRequestModel : MediatR.IRequest<GetHospitalDetailsResponseModel?>
    {
        public Guid HospitalId { get; }

        public GetHospitalDetailsRequestModel(Guid hospitalId)
        {
            HospitalId = hospitalId;
        }
    }
} 