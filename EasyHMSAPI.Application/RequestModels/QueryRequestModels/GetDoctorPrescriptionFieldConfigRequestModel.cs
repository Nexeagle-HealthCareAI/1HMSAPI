using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetDoctorPrescriptionFieldConfigRequestModel : IRequest<GetDoctorPrescriptionFieldConfigResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
