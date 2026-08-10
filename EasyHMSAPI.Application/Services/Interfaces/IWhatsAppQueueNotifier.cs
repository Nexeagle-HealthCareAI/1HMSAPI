namespace EasyHMSAPI.Application.Services.Interfaces
{
    // Best-effort server-to-server push to the WhatsApp gateway's already-built inbound receiver
    // (POST /events/token-called) -- see WhatsAppQueueNotifier for the contract details. A failed
    // push never fails the caller's underlying queue action.
    public interface IWhatsAppQueueNotifier
    {
        Task NotifyTokenCalledAsync(Guid appointmentId, int currentToken, int? estimatedWaitMinutes, CancellationToken cancellationToken);
    }
}
