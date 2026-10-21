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
        [Description("Find live, bookable doctors on Doctor Dekho by name, specialty, degree, distance, city or language. "
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

        // Tool name, result size and latency only -- never the arguments, so nothing a patient
        // typed can end up in the logs even indirectly.
        private void Log(string tool, int results, Stopwatch sw) =>
            _logger.LogInformation("MCP {Tool} returned {Results} in {ElapsedMs} ms", tool, results, sw.ElapsedMilliseconds);
    }
}
