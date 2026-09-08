using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class BulkBatchRowError
    {
        public int RowIndex { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class CreateBulkBatchResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        // New medicines created automatically from an Item Name value, not pre-existing in the catalogue.
        public int CreatedItemCount { get; set; }
        public List<BulkBatchRowError> Errors { get; set; } = new List<BulkBatchRowError>();
    }
}
