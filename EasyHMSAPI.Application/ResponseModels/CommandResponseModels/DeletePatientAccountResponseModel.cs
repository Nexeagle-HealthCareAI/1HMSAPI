using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeletePatientAccountResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; } = 200;
        public int DocumentsDeleted { get; set; }
        public int FamilyMembersDeleted { get; set; }
        /// <summary>Health-locker files the blob store refused to delete; non-zero means "call again".</summary>
        public int DocumentsPending { get; set; }
    }
}
