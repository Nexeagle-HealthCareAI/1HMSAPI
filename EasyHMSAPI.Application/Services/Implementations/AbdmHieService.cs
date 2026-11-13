using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Outbound calls to ABDM's HIE-CM gateway (M2 linking and data flow, later M3). Every call is an
    /// exchange: the row is saved under the REQUEST-ID before sending, so a callback that arrives
    /// ahead of the HTTP response still finds it, and a synchronous refusal is recorded as refused
    /// rather than left looking like it is waiting.
    /// </summary>
    public sealed class AbdmHieService : IAbdmHieService
    {
        public const string MissingHipIdCode = "LOCAL-HIP-ID-MISSING";
        public const string NoResponseCode = "LOCAL-NO-RESPONSE";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly HttpClient _httpClient;
        private readonly IAbdmGatewayService _gateway;
        private readonly IAbdmExchangeLog _log;
        private readonly AppDbContext _db;
        private readonly ILogger<AbdmHieService> _logger;
        private readonly string _host;
        private readonly string _cmId;

        public AbdmHieService(
            HttpClient httpClient,
            IAbdmGatewayService gateway,
            IAbdmExchangeLog log,
            AppDbContext db,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<AbdmHieService> logger)
        {
            _httpClient = httpClient;
            _gateway = gateway;
            _log = log;
            _db = db;
            _logger = logger;
            var isProd = environment.IsProduction();
            _cmId = isProd ? "abdm" : "sbx";
            // Hosts as the abdm-m2 skill's integrate reference publishes them.
            var configured = configuration[isProd ? "Abdm:HieCmHostProd" : "Abdm:HieCmHost"];
            _host = (!string.IsNullOrWhiteSpace(configured)
                ? configured
                : isProd ? "https://apis.abdm.gov.in" : "https://dev.abdm.gov.in").TrimEnd('/');
        }

        public async Task<AbdmHieResult> SendAsync(AbdmHieCall call, CancellationToken cancellationToken)
        {
            var exchange = new AbdmExchange
            {
                HospitalId = call.HospitalId,
                Kind = call.Kind,
                Path = call.Path,
                SubjectRef = call.SubjectRef,
                ExpectsCallback = call.ExpectsCallback
            };

            // A missing facility id is refused here and named: sent anyway, ABDM answers with an
            // entitlement error that reads like a credentials problem.
            var hipId = await _db.AbdmFacilities
                .Where(f => f.HospitalId == call.HospitalId)
                .Select(f => f.HipId)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(hipId))
            {
                const string message = "Nothing was sent to ABDM: this hospital has no ABDM facility (HIP) ID. Set it on the ABHA / ABDM page under Facility (HIP / HFR) ID.";
                await _log.MarkRefusedAsync(exchange, MissingHipIdCode, message, null, cancellationToken);
                throw new AbdmException(message, MissingHipIdCode, 0, exchange.RequestId);
            }
            exchange.FacilityId = hipId;

            try
            {
                var response = await AbdmHttp.SendAsync(_httpClient, _gateway, _logger, async () =>
                {
                    var requestId = Guid.NewGuid().ToString();
                    await _log.RecordAttemptAsync(exchange, requestId, cancellationToken);

                    var accessToken = await _gateway.GetAccessTokenAsync(cancellationToken);
                    var request = new HttpRequestMessage(HttpMethod.Post, _host + call.Path)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(call.Body, JsonOptions), Encoding.UTF8, "application/json")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    AbdmHttp.AddStandardHeaders(request, _cmId, requestId);
                    if (call.FacilityHeader == AbdmFacilityHeader.Hip)
                        request.Headers.Add("X-HIP-ID", hipId);
                    else if (call.FacilityHeader == AbdmFacilityHeader.Hiu)
                        request.Headers.Add("X-HIU-ID", hipId);
                    if (!string.IsNullOrWhiteSpace(call.LinkToken))
                        request.Headers.Add("X-LINK-TOKEN", call.LinkToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    return request;
                }, cancellationToken);

                await _log.MarkSucceededAsync(exchange, response.StatusCode, cancellationToken);
                return new AbdmHieResult
                {
                    ExchangeId = exchange.ExchangeId,
                    RequestId = exchange.RequestId,
                    HttpStatus = response.StatusCode,
                    Body = response.BodyText
                };
            }
            catch (AbdmException ex)
            {
                await _log.MarkRefusedAsync(exchange, ex.Code, ex.Message, ex.StatusCode, cancellationToken);
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // A timeout or dropped connection: nothing is pending at ABDM that we know of.
                var message = $"ABDM did not respond. Please try again shortly. Reference: {exchange.RequestId}";
                _logger.LogWarning(ex, "ABDM {Kind} REQUEST-ID {RequestId} got no response.", call.Kind, exchange.RequestId);
                await _log.MarkRefusedAsync(exchange, NoResponseCode, message, null, CancellationToken.None);
                throw new AbdmException(message, NoResponseCode, 0, exchange.RequestId);
            }
        }
    }
}
