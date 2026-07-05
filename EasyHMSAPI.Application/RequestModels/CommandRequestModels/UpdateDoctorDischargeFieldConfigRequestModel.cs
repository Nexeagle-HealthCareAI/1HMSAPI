using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateDoctorDischargeFieldConfigRequestModel : IRequest<UpdateDoctorDischargeFieldConfigResponseModel>
    {
        [JsonIgnore]
        public Guid DoctorId { get; set; }   // taken from the route, not the body
        public List<DischargeFieldConfigItemModel> Fields { get; set; } = new();
    }
}
