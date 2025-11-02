using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientProfileRequestModel : IRequest<GetPatientProfileResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public string PatientId { get; set; } = string.Empty;
    }
}