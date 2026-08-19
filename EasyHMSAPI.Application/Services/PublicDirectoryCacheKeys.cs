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
        // One cache entry per filter combo (not one whole-platform entry) now that the
        // directory is paginated/filtered — a page-1 "Neurologists in Mumbai" request and a
        // page-2 "all doctors" request are genuinely different result sets, so they must not
        // share a cache slot. Still a plain 60s TTL per entry (see CacheTtl in
        // GetPublicDoctorsHandler); many small entries costs nothing meaningful at this scale.
        public static string PublicDoctorsList(int page, int pageSize, string? city, string? state, string? specialtyCategory, string? search, Guid? hospitalId = null, Guid? doctorId = null) =>
            $"public:doctors:{page}:{pageSize}:{city?.Trim().ToLowerInvariant()}:{state?.Trim().ToLowerInvariant()}:{specialtyCategory?.Trim().ToLowerInvariant()}:{search?.Trim().ToLowerInvariant()}:{hospitalId}:{doctorId}";

        public static string DoctorAvailability(Guid doctorId, DateTime date) =>
            $"public:doctor-availability:{doctorId}:{date:yyyyMMdd}";

        public static string BookedSlots(Guid hospitalId, Guid doctorId, DateTime date) =>
            $"public:booked-slots:{hospitalId}:{doctorId}:{date:yyyyMMdd}";

        // Single whole-platform entry — unlike PublicDoctorsList there's no per-request filter
        // combo to key on, this is just "all bookable categories right now".
        public const string PublicSpecialtiesList = "public:specialties";

        public static string DoctorRoster(Guid hospitalId) => $"public:doctor-roster:{hospitalId}";
    }
}
