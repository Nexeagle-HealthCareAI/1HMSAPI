namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>What WhatsApp said to a send: whether it accepted the message, and the id it gave it. The id comes back later in the
    /// delivery status callback (delivered, read, failed).</summary>
    public class WhatsAppSendResult
    {
        public bool Accepted { get; init; }
        public string? MessageId { get; init; }

        public static WhatsAppSendResult Refused { get; } = new() { Accepted = false };
    }
}
