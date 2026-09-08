using System;
using System.Collections.Generic;
using EasyHMSAPI.Application.Common;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class BulkBatchRowModel
    {
        public string StoreCode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        // Only used when ItemCode doesn't resolve to an existing InventoryItem -- the handler then
        // auto-creates a new catalogue entry (Category=DRUG) from this name instead of rejecting
        // the row, so a brand-new medicine can be added and stocked in one upload.
        public string? ItemName { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? Mrp { get; set; }
        public string? BarcodeValue { get; set; }
        public decimal ReceivedQty { get; set; }
    }

    public class CreateBulkBatchRequestModel : IRequest<CreateBulkBatchResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string LoggedInUserName { get; set; } = string.Empty;
        
        public List<BulkBatchRowModel> Rows { get; set; } = new List<BulkBatchRowModel>();
    }
}
