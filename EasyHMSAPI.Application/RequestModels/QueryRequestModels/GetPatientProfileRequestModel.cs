using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPatientProfileRequestModel : IRequest<GetPatientProfileResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public string PatientId { get; set; } = string.Empty;
    }
}