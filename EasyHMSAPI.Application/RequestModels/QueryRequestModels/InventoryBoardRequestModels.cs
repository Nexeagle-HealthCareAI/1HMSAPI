using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // One consolidated query for the Inventory Management board — stock-by-store, expiry alerts
    // (90/60/30-day tiers), and reorder alerts, same "one board query" shape as GetOtBoardRequestModel.
    [ExcludeFromCodeCoverage]
    public class GetInventoryBoardRequestModel : IRequest<GetInventoryBoardResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
