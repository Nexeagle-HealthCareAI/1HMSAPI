namespace EasyHMSAPI.Application.Helpers.Interfaces
{
    public record SubscriptionLimitResult(bool Allowed, string? Reason);

    public interface ISubscriptionLimitHelper
    {
        Task<SubscriptionLimitResult> CanAddDoctorAsync(Guid hospitalId, CancellationToken cancellationToken);
        Task<SubscriptionLimitResult> CanAddBedsAsync(Guid hospitalId, int additionalBeds, CancellationToken cancellationToken);
    }
}
