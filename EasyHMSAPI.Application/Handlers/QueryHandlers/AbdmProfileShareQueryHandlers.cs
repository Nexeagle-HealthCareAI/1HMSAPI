using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAbdmProfileSharesHandler : IRequestHandler<GetAbdmProfileSharesRequestModel, GetAbdmProfileSharesResponseModel>
    {
        private const int MaxItems = 100;
        private readonly AppDbContext _context;

        public GetAbdmProfileSharesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAbdmProfileSharesResponseModel> Handle(GetAbdmProfileSharesRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new GetAbdmProfileSharesResponseModel { Success = false, Message = "hospitalId is required." };

            var query = _context.AbdmProfileShares.AsNoTracking().Where(s => s.HospitalId == request.HospitalId);
            if (!string.IsNullOrWhiteSpace(request.CounterId))
            {
                var counter = request.CounterId.Trim();
                query = query.Where(s => s.CounterId == counter);
            }
            var status = request.Status?.Trim().ToUpperInvariant();
            if (status == "NEW" || status == "HANDLED")
                query = query.Where(s => s.StatusCode == status);

            var shares = await query.OrderByDescending(s => s.ReceivedAt).Take(MaxItems).ToListAsync(cancellationToken);

            // Returning vs new: does a non-merged patient here already carry this ABHA number? Stored
            // formats can differ (plain 14 digits vs 91-xxxx-xxxx-xxxx), so match on every variant in
            // SQL and compare on digits only in memory.
            var wanted = shares.Where(s => !string.IsNullOrWhiteSpace(s.AbhaNumber))
                .SelectMany(s => Variants(s.AbhaNumber!)).Distinct().ToList();

            var byDigits = new Dictionary<string, (string PatientId, string? Name)>();
            if (wanted.Count > 0)
            {
                var patients = await _context.PatientRegistrations.AsNoTracking()
                    .Where(p => p.HospitalId == request.HospitalId
                                && p.MergedIntoPatientId == null
                                && p.PatientId != null
                                && p.AbhaId != null
                                && wanted.Contains(p.AbhaId))
                    .Select(p => new { p.PatientId, p.FullName, p.AbhaId })
                    .ToListAsync(cancellationToken);
                foreach (var p in patients)
                    byDigits.TryAdd(Digits(p.AbhaId!), (p.PatientId!, p.FullName));
            }

            var items = shares.Select(s =>
            {
                (string PatientId, string? Name) match = default;
                var found = !string.IsNullOrWhiteSpace(s.AbhaNumber) && byDigits.TryGetValue(Digits(s.AbhaNumber!), out match);
                return new AbdmProfileShareItem
                {
                    ProfileShareId = s.ProfileShareId,
                    CounterId = s.CounterId,
                    AbhaNumber = s.AbhaNumber,
                    AbhaAddress = s.AbhaAddress,
                    FullName = s.FullName,
                    Gender = s.Gender,
                    DateOfBirth = s.DateOfBirth,
                    Mobile = s.Mobile,
                    Address = s.Address,
                    StatusCode = s.StatusCode,
                    ReceivedAt = s.ReceivedAt,
                    ExistingPatientId = found ? match.PatientId : null,
                    ExistingPatientName = found ? match.Name : null
                };
            }).ToList();

            return new GetAbdmProfileSharesResponseModel { Success = true, Items = items };
        }

        private static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

        // Raw, digits-only, and 14-digit dashed (XX-XXXX-XXXX-XXXX) forms of an ABHA number.
        private static IEnumerable<string> Variants(string abha)
        {
            var raw = abha.Trim();
            yield return raw;
            var d = Digits(raw);
            if (d.Length == 0) yield break;
            yield return d;
            if (d.Length == 14) yield return $"{d[..2]}-{d.Substring(2, 4)}-{d.Substring(6, 4)}-{d.Substring(10, 4)}";
        }
    }

    public class GetAbdmFacilityHandler : IRequestHandler<GetAbdmFacilityRequestModel, GetAbdmFacilityResponseModel>
    {
        private const string SandboxQrBaseUrl = "https://phrsbx.abdm.gov.in/share-profile";
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public GetAbdmFacilityHandler(AppDbContext context, IConfiguration configuration, IHostEnvironment environment)
        {
            _context = context;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task<GetAbdmFacilityResponseModel> Handle(GetAbdmFacilityRequestModel request, CancellationToken cancellationToken)
        {
            var hipId = await _context.AbdmFacilities.AsNoTracking()
                .Where(f => f.HospitalId == request.HospitalId)
                .Select(f => f.HipId)
                .FirstOrDefaultAsync(cancellationToken);

            // Production has no default — the live PHR share URL must be set explicitly, never guessed.
            var configured = _configuration["Abdm:PhrShareBaseUrl"];
            var qrBase = !string.IsNullOrWhiteSpace(configured)
                ? configured.TrimEnd('/')
                : _environment.IsProduction() ? string.Empty : SandboxQrBaseUrl;

            return new GetAbdmFacilityResponseModel { Success = true, HipId = hipId, QrBaseUrl = qrBase };
        }
    }
}
