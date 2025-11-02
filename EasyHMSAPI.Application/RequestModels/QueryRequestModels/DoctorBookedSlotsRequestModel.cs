using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorBookedSlotsRequestModel : IRequest<DoctorBookedSlotsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime Date { get; set; }
    }
}
