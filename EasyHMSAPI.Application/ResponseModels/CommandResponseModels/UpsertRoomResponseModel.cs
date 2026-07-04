using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertRoomResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid RoomId { get; set; }
        public string? RoomNo { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
