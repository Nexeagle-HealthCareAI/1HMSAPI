using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAbdmM2ReadinessHandler : IRequestHandler<GetAbdmM2ReadinessRequestModel, AbdmM2ReadinessResponseModel>
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;

        public GetAbdmM2ReadinessHandler(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public async Task<AbdmM2ReadinessResponseModel> Handle(GetAbdmM2ReadinessRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new AbdmM2ReadinessResponseModel { Success = false, Message = "Hospital is required." };

            var hipId = await _db.AbdmFacilities.Where(f => f.HospitalId == request.HospitalId)
                .Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);
            var bridge = IsSet(_configuration["Abdm:PublicBaseUrl"]) && IsSet(_configuration["Abdm:CallbackSecret"]);
            var lastCallback = await _db.AbdmInbounds
                .Where(i => i.HospitalId == request.HospitalId && i.Verdict == AbdmInboundVerdict.Accepted)
                .OrderByDescending(i => i.ReceivedAt)
                .Select(i => (DateTime?)i.ReceivedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var response = new AbdmM2ReadinessResponseModel
            {
                Success = true,
                HipId = string.IsNullOrWhiteSpace(hipId) ? null : hipId,
                BridgeSettingsPresent = bridge,
                LastCallbackAt = lastCallback
            };

            // The two are deliberately not alike: without a HIP ID nothing is sent, but without a
            // callback URL calls are sent and accepted and their answers have nowhere to go.
            if (response.HipId == null)
                response.Issues.Add(new AbdmReadinessIssue { Level = "blocker", Text = "No facility (HIP) ID is saved for this hospital, so linking is refused before anything is sent. Save it under Facility (HIP / HFR) ID." });
            if (!bridge)
                response.Issues.Add(new AbdmReadinessIssue { Level = "warning", Text = "The callback (bridge) URL is not configured on this server (Abdm:PublicBaseUrl, Abdm:CallbackSecret). ABDM will accept requests, but its answers will never arrive." });
            else if (lastCallback == null)
                response.Issues.Add(new AbdmReadinessIssue { Level = "warning", Text = "No answer from ABDM has reached this server yet. If requests stay \"waiting\", press Register bridge so ABDM knows where to send them." });
            return response;
        }

        private static bool IsSet(string? value) => !string.IsNullOrWhiteSpace(value) && !value.StartsWith('<');
    }

    public class GetAbdmExchangesHandler : IRequestHandler<GetAbdmExchangesRequestModel, AbdmExchangeListResponseModel>
    {
        private readonly AppDbContext _db;
        private readonly int _windowSeconds;

        public GetAbdmExchangesHandler(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            // ABDM publishes no timeout for any callback; this is only the window we wait before
            // calling a silence "no answer".
            _windowSeconds = int.TryParse(configuration["Abdm:CallbackWindowSeconds"], out var s) && s > 0 ? s : 60;
        }

        public async Task<AbdmExchangeListResponseModel> Handle(GetAbdmExchangesRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new AbdmExchangeListResponseModel { Success = false, Message = "Hospital is required." };

            var take = Math.Clamp(request.Take, 1, 500);
            var rows = await _db.AbdmExchanges.AsNoTracking()
                .Where(e => e.HospitalId == request.HospitalId)
                .OrderByDescending(e => e.SentAt)
                .Take(take)
                .ToListAsync(cancellationToken);

            var inboundIds = rows.Where(e => e.AnsweredByInboundId != null).Select(e => e.AnsweredByInboundId!.Value).ToList();
            var callbacks = await _db.AbdmInbounds.AsNoTracking()
                .Where(i => inboundIds.Contains(i.InboundId))
                .Select(i => new { i.InboundId, i.Path, i.HeaderNames })
                .ToDictionaryAsync(i => i.InboundId, cancellationToken);

            var now = DateTime.UtcNow;
            return new AbdmExchangeListResponseModel
            {
                Success = true,
                CallbackWindowSeconds = _windowSeconds,
                Exchanges = rows.Select(e =>
                {
                    var callback = e.AnsweredByInboundId != null && callbacks.TryGetValue(e.AnsweredByInboundId.Value, out var c) ? c : null;
                    var silent = e.Status == AbdmExchangeStatus.Accepted && (now - (e.AcceptedAt ?? e.SentAt)).TotalSeconds > _windowSeconds;
                    return new AbdmExchangeDto
                    {
                        RequestId = e.RequestId,
                        Kind = e.Kind,
                        SubjectRef = e.SubjectRef,
                        Status = silent ? "NO_ANSWER" : e.Status,
                        HttpStatus = e.HttpStatus,
                        ErrorCode = e.ErrorCode,
                        ErrorMessage = e.ErrorMessage,
                        SentAt = e.SentAt,
                        AcceptedAt = e.AcceptedAt,
                        AnsweredAt = e.AnsweredAt,
                        WaitMs = e.AnsweredAt != null ? (long)(e.AnsweredAt.Value - e.SentAt).TotalMilliseconds : null,
                        CallbackPath = callback?.Path,
                        CallbackHeaders = callback?.HeaderNames
                    };
                }).ToList()
            };
        }
    }

    public class GetAbdmInboundRequestsHandler : IRequestHandler<GetAbdmInboundRequestsRequestModel, AbdmInboundListResponseModel>
    {
        private readonly AppDbContext _db;

        public GetAbdmInboundRequestsHandler(AppDbContext db)
        {
            _db = db;
        }

        public async Task<AbdmInboundListResponseModel> Handle(GetAbdmInboundRequestsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new AbdmInboundListResponseModel { Success = false, Message = "Hospital is required." };

            var take = Math.Clamp(request.Take, 1, 500);
            var rows = await _db.AbdmInbounds.AsNoTracking()
                .Where(i => i.HospitalId == request.HospitalId && i.AnswersRequestId == null)
                .OrderByDescending(i => i.ReceivedAt)
                .Take(take)
                .ToListAsync(cancellationToken);

            return new AbdmInboundListResponseModel
            {
                Success = true,
                Requests = rows.Select(i => new AbdmInboundDto
                {
                    Path = i.Path,
                    Verdict = i.Verdict,
                    FacilityId = i.FacilityId,
                    HeaderNames = i.HeaderNames,
                    ReceivedAt = i.ReceivedAt,
                    ProcessedAt = i.ProcessedAt,
                    ProcessError = i.ProcessError,
                    Unanswered = i.Verdict == AbdmInboundVerdict.Accepted && i.ProcessedAt == null
                }).ToList()
            };
        }
    }
}
