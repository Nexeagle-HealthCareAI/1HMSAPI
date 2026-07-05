using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetEquipmentListRequestModel : IRequest<GetEquipmentListResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
        public string? Department { get; set; }
        public string? Category { get; set; }
        public bool DueOnly { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetMaintenanceLogHistoryRequestModel : IRequest<GetMaintenanceLogHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid EquipmentId { get; set; }
    }
}
