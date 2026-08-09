using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetNurseRosterRequestModel : IRequest<GetNurseRosterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? WardCode { get; set; }
        public string? ShiftCode { get; set; }
        public Guid? NurseUserId { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }
}
