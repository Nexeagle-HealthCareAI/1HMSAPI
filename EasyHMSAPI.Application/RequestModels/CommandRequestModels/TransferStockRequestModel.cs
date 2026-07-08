using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class TransferStockRequestModel : IRequest<TransferStockResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid FromStoreId { get; set; }
        public Guid ToStoreId { get; set; }
        public Guid? BatchId { get; set; }
        public decimal Qty { get; set; }
        public string? Notes { get; set; }

        public string? LoggedInUserName { get; set; }
        public Guid? LoggedInUserId { get; set; }
    }
}
