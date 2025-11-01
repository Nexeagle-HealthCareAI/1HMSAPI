namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class RescheduleAppointmentResponseModel
    {
        public Guid ApptId { get; set; }
        public string? FinalStatus { get; set; }
        public TokenInfo? Token { get; set; }
        public bool? IsReminderSent { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class TokenInfo
    {
        public int TokenNo { get; set; }
        public DateTime TokenDate { get; set; }
    }
}
