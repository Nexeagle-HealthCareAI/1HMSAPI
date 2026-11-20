using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmOtpTxnResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TxnId { get; set; }
        /// <summary>No ABHA on this mobile or Aadhaar: the screen offers to create one (VRFY_ABHA_303, 403).</summary>
        public bool NotFound { get; set; }
    }
}
