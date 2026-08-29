using System;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class ExportBankFileResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public byte[]? FileBytes { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/csv";
    }
}
