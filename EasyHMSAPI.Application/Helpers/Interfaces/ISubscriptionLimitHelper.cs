namespace EasyHMSAPI.Application.Helpers.Interfaces
{
    public record SubscriptionLimitResult(bool Allowed, string? Reason);

    public record SubscriptionUsage(
        int? MaxDoctors,
        int CurrentDoctors,
        int? MaxBeds,
        int CurrentBeds);

    public interface ISubscriptionLimitHelper
    {
        Task<SubscriptionLimitResult> CanAddDoctorAsync(Guid hospitalId, CancellationToken cancellationToken);
        Task<SubscriptionLimitResult> CanAddBedsAsync(Guid hospitalId, int additionalBeds, CancellationToken cancellationToken);
        Task<SubscriptionUsage> GetUsageAsync(Guid hospitalId, CancellationToken cancellationToken);
    }
}
