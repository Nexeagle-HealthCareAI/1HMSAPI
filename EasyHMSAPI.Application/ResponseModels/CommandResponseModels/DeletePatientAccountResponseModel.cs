using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeletePatientAccountResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>Record files that couldn't be removed from storage this time; the call can simply be repeated.</summary>
        public int DocumentsPending { get; set; }
    }
}
