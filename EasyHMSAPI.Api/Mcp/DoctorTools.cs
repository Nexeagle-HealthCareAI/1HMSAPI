using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace EasyHMSAPI.Api.Mcp
{
    /// <summary>
    /// Read-only doctor tools for Vita (the NLP router's patient agent), served at POST /mcp.
    /// Every tool goes through the same MediatR queries as the public Doctor Dekho directory, so
    /// listing rules (live, opted in, not delisted) live in one place. No tool writes, books or
    /// holds a slot, and no tool receives patient text -- inputs are filters and a place only.
    /// </summary>
    [McpServerToolType]
    public sealed class DoctorTools
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DoctorTools> _logger;
        private readonly string? _profileUrlTemplate;

        public DoctorTools(IMediator mediator, ILogger<DoctorTools> logger, IConfiguration configuration)
        {
            _mediator = mediator;
            _logger = logger;
            var template = configuration["Mcp:DoctorProfileUrlTemplate"];
            _profileUrlTemplate = string.IsNullOrWhiteSpace(template) || template.StartsWith("<") ? null : template;
        }

        [McpServerTool(Name = "list_specialties", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("All specialty categories that currently have bookable doctors. Use specialtyId in search_doctors.")]
        public async Task<McpSpecialtyList> ListSpecialties(CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var response = await _mediator.Send(new GetPublicSpecialtiesRequestModel(), cancellationToken);
            var result = new McpSpecialtyList(response.Specialties
                .Select(s => new McpSpecialty(s.Category, s.DisplayName ?? s.Category, s.DoctorCount, Array.Empty<string>()))
                .ToList());
            Log("list_specialties", result.Specialties.Count, sw);
            return result;
        }

        [McpServerTool(Name = "search_doctors", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Find live, bookable doctors on Doctor Dekho by name, specialty, degree, distance, city, language, hospital, gender or today's availability. "
                   + "Send at least one of: query, specialtyIds, degree, lat+lng, hospitalId. Results are already ranked.")]
        public async Task<McpDoctorSearchResult> SearchDoctors(
            [Description("Doctor name, spelling tolerant, e.g. \"dr sarma\"")] string? query = null,
            [Description("Values from list_specialties, e.g. [\"Cardiologist\"]")] string[]? specialtyIds = null,
            [Description("One degree: MBBS, MD, MS, DM, MCh, DNB, BDS, MDS, DGO, DCH ...")] string? degree = null,
            [Description("Patient latitude (WGS84)")] double? lat = null,
            [Description("Patient longitude (WGS84)")] double? lng = null,
            [Description("Radius in km when lat/lng are sent; default 5")] double? maxDistanceKm = null,
            [Description("Hospital city, e.g. \"Kolkata\"")] string? city = null,
            [Description("Vita running in this hospital's own channel: its doctors come first, others follow")] Guid? hospitalId = null,
            [Description("Language the doctor speaks, e.g. \"Bengali\"")] string? language = null,
            [Description("relevance (default) | distance | experience")] string? sort = null,
            [Description("1 to 10, default 5")] int limit = 5,
            [Description("Skip this many ranked doctors (\"show more\"), 0 to 90")] int offset = 0,
            [Description("With hospitalId: only that hospital's doctors")] bool onlyAtHospital = false,
            [Description("female | male: a patient asking for a lady (or male) doctor")] string? gender = null,
            [Description("Only doctors working today (IST)")] bool availableToday = false,
            CancellationToken cancellationToken = default)
        {
            if (lat.HasValue != lng.HasValue)
                throw new McpException("Send lat and lng together.");
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
                throw new McpException("lat/lng out of range.");
            var hasFilter = !string.IsNullOrWhiteSpace(query)
                            || specialtyIds is { Length: > 0 }
                            || !string.IsNullOrWhiteSpace(degree)
                            || lat.HasValue
                            || hospitalId.HasValue;
            if (!hasFilter)
                throw new McpException("Send at least one of: query, specialtyIds, degree, lat+lng, hospitalId.");
            var sortKey = string.IsNullOrWhiteSpace(sort) ? "relevance" : sort.Trim().ToLowerInvariant();
            if (sortKey is not ("relevance" or "distance" or "experience"))
                throw new McpException("sort must be relevance, distance or experience.");
            if (offset is < 0 or > EasyHMSAPI.Application.Handlers.QueryHandlers.SearchPublicDoctorsHandler.MaxOffset)
                throw new McpException("offset must be 0 to 90.");
            if (onlyAtHospital && !hospitalId.HasValue)
                throw new McpException("onlyAtHospital needs hospitalId.");
            if (!string.IsNullOrWhiteSpace(gender) && gender.Trim().ToLowerInvariant() is not ("female" or "male" or "f" or "m"))
                throw new McpException("gender must be female or male.");

            var sw = Stopwatch.StartNew();
            var response = await _mediator.Send(new SearchPublicDoctorsRequestModel
            {
                Query = query,
                SpecialtyCategories = specialtyIds?.ToList(),
                Degree = degree,
                Latitude = lat,
                Longitude = lng,
                MaxDistanceKm = maxDistanceKm,
                City = city,
                HospitalId = hospitalId,
                Language = language,
                Sort = sortKey,
                Limit = limit,
                Offset = offset,
                OnlyAtHospital = onlyAtHospital,
                Gender = gender,
                AvailableToday = availableToday,
            }, cancellationToken);

            var result = new McpDoctorSearchResult(
                response.Doctors.Select(m => m.Doctor.ToMcp(_profileUrlTemplate, m.DistanceKm, m.SameHospital, m.NameMatchScore)).ToList(),
                response.Total);
            Log("search_doctors", result.Doctors.Count, sw);
            return result;
        }

        [McpServerTool(Name = "get_doctor", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("One live doctor's full public profile. found=false when the doctor is not (or no longer) listed.")]
        public async Task<McpDoctorLookup> GetDoctor(
            [Description("doctorId from search_doctors")] Guid doctorId,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var response = await _mediator.Send(new GetPublicDoctorsRequestModel { DoctorId = doctorId, Page = 1, PageSize = 1 }, cancellationToken);
            var doctor = response.Doctors.FirstOrDefault();
            var result = new McpDoctorLookup(doctor != null, doctor?.ToMcp(_profileUrlTemplate));
            Log("get_doctor", doctor == null ? 0 : 1, sw);
            return result;
        }

        [McpServerTool(Name = "list_doctor_names", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("doctorId and name of every live doctor, for building a name index. Optional city filter.")]
        public async Task<McpDoctorNameList> ListDoctorNames(
            [Description("Hospital city, e.g. \"Kolkata\"")] string? city = null,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var response = await _mediator.Send(new ListPublicDoctorNamesRequestModel { City = city }, cancellationToken);
            var result = new McpDoctorNameList(response.Doctors.Select(d => new McpDoctorName(d.DoctorId, d.FullName, d.City)).ToList());
            Log("list_doctor_names", result.Doctors.Count, sw);
            return result;
        }

        [McpServerTool(Name = "search_hospitals", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Find hospitals with doctors on Doctor Dekho by name (spelling tolerant, the city may be part of the query, "
                   + "e.g. \"Sadar Hospital Purnia\"), city or distance. Then list a hospital's doctors with "
                   + "search_doctors(hospitalId, onlyAtHospital=true). Send at least one of: query, city, lat+lng.")]
        public async Task<McpHospitalSearchResult> SearchHospitals(
            [Description("Hospital name as the patient said it")] string? query = null,
            [Description("City, e.g. \"Purnia\"")] string? city = null,
            [Description("Patient latitude (WGS84)")] double? lat = null,
            [Description("Patient longitude (WGS84)")] double? lng = null,
            [Description("Radius in km for a search without a name; default 25")] double? maxDistanceKm = null,
            [Description("1 to 10, default 5")] int limit = 5,
            CancellationToken cancellationToken = default)
        {
            if (lat.HasValue != lng.HasValue)
                throw new McpException("Send lat and lng together.");
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
                throw new McpException("lat/lng out of range.");
            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(city) && !lat.HasValue)
                throw new McpException("Send at least one of: query, city, lat+lng.");

            var sw = Stopwatch.StartNew();
            var response = await _mediator.Send(new SearchPublicHospitalsRequestModel
            {
                Query = query, City = city, Latitude = lat, Longitude = lng, MaxDistanceKm = maxDistanceKm, Limit = limit,
            }, cancellationToken);
            var result = new McpHospitalSearchResult(
                response.Hospitals.Select(h => new McpHospitalMatch(h.HospitalId, h.Name, h.Address, h.City, h.State, h.Pincode,
                    h.Contact, h.Latitude, h.Longitude, h.DistanceKm, h.NameMatchScore, h.DoctorCount)).ToList(),
                response.Total);
            Log("search_hospitals", result.Hospitals.Count, sw);
            return result;
        }

        [McpServerTool(Name = "get_doctor_availability", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Which days a doctor works, from a date (IST), and the next working day: \"Is Dr Sharma available Sunday?\". "
                   + "Days and shift hours only; the hospital confirms the exact time of a booking.")]
        public async Task<McpDoctorAvailability> GetDoctorAvailability(
            [Description("doctorId from search_doctors")] Guid doctorId,
            [Description("First day, yyyy-MM-dd (IST); default today")] string? fromDate = null,
            [Description("How many days from fromDate, 1 to 14; default 7")] int days = 7,
            CancellationToken cancellationToken = default)
        {
            var todayIst = DateTime.UtcNow.AddMinutes(330).Date;
            var start = todayIst;
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                if (!DateTime.TryParseExact(fromDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out start))
                    throw new McpException("fromDate must be yyyy-MM-dd.");
                if (start < todayIst) throw new McpException("fromDate cannot be in the past.");
                if (start > todayIst.AddDays(90)) throw new McpException("fromDate must be within 90 days.");
            }
            days = Math.Clamp(days, 1, 14);

            var sw = Stopwatch.StartNew();
            var list = new List<McpAvailabilityDay>(days);
            for (var i = 0; i < days; i++)
            {
                var date = start.AddDays(i);
                var r = await _mediator.Send(new GetPublicDoctorAvailabilityRequestModel { DoctorId = doctorId, Date = date }, cancellationToken);
                if (!r.Success)
                {
                    Log("get_doctor_availability", 0, sw);
                    return new McpDoctorAvailability(false, doctorId, Array.Empty<McpAvailabilityDay>(), null, AvailabilityNote);
                }
                // The leave reason is the doctor's own note: never passed on, only "not available".
                list.Add(new McpAvailabilityDay(date.ToString("yyyy-MM-dd"), r.IsAvailable,
                    r.IsAvailable
                        ? r.Shifts.Select(s => new McpShift(s.Name, Hhmm(s.StartTime), Hhmm(s.EndTime))).ToList()
                        : (IReadOnlyList<McpShift>)Array.Empty<McpShift>()));
            }
            var next = list.FirstOrDefault(d => d.IsAvailable)?.Date;
            Log("get_doctor_availability", list.Count, sw);
            return new McpDoctorAvailability(true, doctorId, list, next, AvailabilityNote);
        }

        private const string AvailabilityNote = "Working days and shift hours only; the hospital confirms the exact appointment time.";

        private static string? Hhmm(TimeSpan? t) => t.HasValue ? t.Value.ToString(@"hh\:mm") : null;

        // Tool name, result size and latency only -- never the arguments, so nothing a patient
        // typed can end up in the logs even indirectly.
        private void Log(string tool, int results, Stopwatch sw) =>
            _logger.LogInformation("MCP {Tool} returned {Results} in {ElapsedMs} ms", tool, results, sw.ElapsedMilliseconds);
    }
}
