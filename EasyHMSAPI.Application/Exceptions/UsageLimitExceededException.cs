namespace EasyHMSAPI.Application.Exceptions
{
    // Thrown by handlers whose response model has no Success flag to signal a blocked action with
    // (e.g. RegisterAppointmentHandler, which throws on every failure path already) -- a clean,
    // user-facing message for the free-tier monthly quota being exhausted. Callers that DO have a
    // Success flag return { Success = false, Message = ... } directly instead of throwing this.
    public class UsageLimitExceededException : Exception
    {
        public UsageLimitExceededException(string message) : base(message)
        {
        }
    }
}
