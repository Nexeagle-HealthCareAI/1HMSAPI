namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Cache key builders shared between the handlers that READ these entries (GetPublicDoctorsHandler,
    /// GetPublicDoctorAvailabilityHandler, DoctorBookedSlotsHandler) and the ones that must INVALIDATE
    /// them on a write (PublicBookAppointmentHandler, ConfirmPreAppointmentHandler) — keeping key
    /// construction in one place means a reader and an invalidator can never silently drift apart and
    /// stop matching each other.
    ///
    /// Process-local IMemoryCache, matching the existing pattern in HospitalAccessFilter — correct for
    /// today's single-instance deployment. If this API is ever scaled to multiple instances, this
    /// needs to move to a distributed cache (e.g. Redis), same caveat as the rate limiter.
    /// </summary>
    public static class PublicDirectoryCacheKeys
    {
        public static string PublicDoctorsList() => "public:doctors:all";

        public static string DoctorAvailability(Guid doctorId, DateTime date) =>
            $"public:doctor-availability:{doctorId}:{date:yyyyMMdd}";

        public static string BookedSlots(Guid hospitalId, Guid doctorId, DateTime date) =>
            $"public:booked-slots:{hospitalId}:{doctorId}:{date:yyyyMMdd}";
    }
}
