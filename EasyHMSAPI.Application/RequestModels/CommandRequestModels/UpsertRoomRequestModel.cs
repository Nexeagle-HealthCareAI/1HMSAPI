using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertRoomRequestModel : IRequest<UpsertRoomResponseModel>
    {
        public Guid? RoomId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        [Required]
        public string? FloorNo { get; set; }
        [Required]
        public string? RoomNo { get; set; }
        public string? RoomType { get; set; }
        public int CapacityInRoom { get; set; } = 1;
        public decimal DailyRate { get; set; }
        public bool IsActive { get; set; } = true;
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
