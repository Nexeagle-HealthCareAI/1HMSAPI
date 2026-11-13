using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>A delivery to our bridge URL as it arrived: the path ABDM appended, every header,
    /// the raw body, and whether it carried a bearer token.</summary>
    public sealed record AbdmInboundDelivery(string Path, IReadOnlyList<KeyValuePair<string, string>> Headers, string Body);

    /// <summary>Reads the facts the abdm-m2 skill's callback checks need, without trusting the body
    /// to have any particular shape.</summary>
    public static class AbdmCallbackInspector
    {
        private const int MaxHeaderSummary = 1000;

        /// <summary>Header names with value lengths, never values — a value can be a credential.</summary>
        public static string DescribeHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            var text = string.Join("; ", headers
                .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                .Select(h => $"{h.Key}:{h.Value?.Length ?? 0}"));
            return text.Length > MaxHeaderSummary ? text[..MaxHeaderSummary] : text;
        }

        public static bool HasBearer(IEnumerable<KeyValuePair<string, string>> headers) =>
            headers.Any(h => string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                && h.Value != null
                && h.Value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && h.Value.Length > "Bearer ".Length);

        public static string? Header(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            var value = headers.FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>X-HIP-ID, else X-HIU-ID: the facility a callback says it is for.</summary>
        public static string? FacilityId(IEnumerable<KeyValuePair<string, string>> headers) =>
            Header(headers, "X-HIP-ID") ?? Header(headers, "X-HIU-ID");

        /// <summary>The REQUEST-ID of the call this delivery answers, from response.requestId (V3) or
        /// resp.requestId (older shape). Null for a delivery that answers nothing, such as a discovery.</summary>
        public static string? AnswersRequestId(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return null;
                foreach (var envelope in new[] { "response", "resp" })
                {
                    if (doc.RootElement.TryGetProperty(envelope, out var r) && r.ValueKind == JsonValueKind.Object
                        && r.TryGetProperty("requestId", out var id) && id.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(id.GetString()))
                        return id.GetString()!.Trim();
                }
            }
            catch (JsonException)
            {
            }
            return null;
        }

        /// <summary>An "error" object in a callback body: the outcome was a refusal even though the
        /// delivery itself arrived fine.</summary>
        public static (string? Code, string? Message)? CallbackError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object)
                    return AbdmHttp.ParseError(error.GetRawText());
            }
            catch (JsonException)
            {
            }
            return null;
        }

        /// <summary>The three checks, in the order the skill gives them. A callback that answers a
        /// call must answer one we sent; a callback that answers nothing must name a facility we serve.</summary>
        public static string Decide(bool hasBearer, string? facilityId, bool facilityKnown, string? answersRequestId, AbdmExchange? answered)
        {
            if (!hasBearer)
                return AbdmInboundVerdict.NoBearer;
            if (facilityId != null && !facilityKnown)
                return AbdmInboundVerdict.UnknownFacility;
            if (answersRequestId != null)
            {
                if (answered == null)
                    return AbdmInboundVerdict.UnknownRequest;
                return answered.Status == AbdmExchangeStatus.Answered
                    ? AbdmInboundVerdict.Duplicate
                    : AbdmInboundVerdict.Accepted;
            }
            return facilityId == null ? AbdmInboundVerdict.UnknownFacility : AbdmInboundVerdict.Accepted;
        }
    }

    public interface IAbdmExchangeLog
    {
        /// <summary>Stamps the REQUEST-ID of the attempt about to be sent and saves the row first, so
        /// a callback that beats the HTTP response still finds its exchange.</summary>
        Task RecordAttemptAsync(AbdmExchange exchange, string requestId, CancellationToken cancellationToken);
        Task MarkSucceededAsync(AbdmExchange exchange, int httpStatus, CancellationToken cancellationToken);
        Task MarkRefusedAsync(AbdmExchange exchange, string? code, string message, int? httpStatus, CancellationToken cancellationToken);
        Task<AbdmInbound> RecordInboundAsync(AbdmInboundDelivery delivery, CancellationToken cancellationToken);
    }

    public sealed class AbdmExchangeLog : IAbdmExchangeLog
    {
        private const int MaxStoredBody = 64_000;
        private readonly AppDbContext _db;

        public AbdmExchangeLog(AppDbContext db)
        {
            _db = db;
        }

        public async Task RecordAttemptAsync(AbdmExchange exchange, string requestId, CancellationToken cancellationToken)
        {
            exchange.RequestId = requestId;
            exchange.SentAt = DateTime.UtcNow;
            exchange.Status = AbdmExchangeStatus.Sent;
            if (exchange.ExchangeId == Guid.Empty)
            {
                exchange.ExchangeId = Guid.NewGuid();
                _db.AbdmExchanges.Add(exchange);
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkSucceededAsync(AbdmExchange exchange, int httpStatus, CancellationToken cancellationToken)
        {
            // The callback may already have landed (and been saved by another scope) while we waited
            // for this response; never move an answered exchange back to "waiting".
            await _db.Entry(exchange).ReloadAsync(cancellationToken);
            exchange.HttpStatus = httpStatus;
            exchange.AcceptedAt ??= DateTime.UtcNow;
            if (exchange.Status == AbdmExchangeStatus.Sent)
                exchange.Status = exchange.ExpectsCallback ? AbdmExchangeStatus.Accepted : AbdmExchangeStatus.Done;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkRefusedAsync(AbdmExchange exchange, string? code, string message, int? httpStatus, CancellationToken cancellationToken)
        {
            exchange.Status = AbdmExchangeStatus.Refused;
            exchange.ErrorCode = code;
            exchange.ErrorMessage = message.Length > 500 ? message[..500] : message;
            exchange.HttpStatus = httpStatus;
            if (exchange.ExchangeId == Guid.Empty)
            {
                exchange.ExchangeId = Guid.NewGuid();
                if (string.IsNullOrEmpty(exchange.RequestId))
                    exchange.RequestId = "local-" + Guid.NewGuid();
                exchange.SentAt = DateTime.UtcNow;
                _db.AbdmExchanges.Add(exchange);
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<AbdmInbound> RecordInboundAsync(AbdmInboundDelivery delivery, CancellationToken cancellationToken)
        {
            var facilityId = AbdmCallbackInspector.FacilityId(delivery.Headers);
            Guid? hospitalId = null;
            if (facilityId != null)
            {
                var normalised = facilityId.ToLower();
                hospitalId = await _db.AbdmFacilities
                    .Where(f => f.HipId.ToLower() == normalised)
                    .Select(f => (Guid?)f.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var answersRequestId = AbdmCallbackInspector.AnswersRequestId(delivery.Body);
            var exchange = answersRequestId == null
                ? null
                : await _db.AbdmExchanges.FirstOrDefaultAsync(e => e.RequestId == answersRequestId, cancellationToken);

            var verdict = AbdmCallbackInspector.Decide(
                AbdmCallbackInspector.HasBearer(delivery.Headers), facilityId, hospitalId != null, answersRequestId, exchange);
            var keep = verdict is AbdmInboundVerdict.Accepted or AbdmInboundVerdict.Duplicate;

            var inbound = new AbdmInbound
            {
                InboundId = Guid.NewGuid(),
                Path = delivery.Path.Length > 300 ? delivery.Path[..300] : delivery.Path,
                HeaderRequestId = AbdmCallbackInspector.Header(delivery.Headers, "REQUEST-ID"),
                AnswersRequestId = answersRequestId,
                FacilityId = facilityId,
                HospitalId = hospitalId ?? exchange?.HospitalId,
                HeaderNames = AbdmCallbackInspector.DescribeHeaders(delivery.Headers),
                // A rejected delivery is logged, never stored: it may be another organisation's patient.
                RawBody = keep ? Truncate(delivery.Body) : null,
                Verdict = verdict,
                ReceivedAt = DateTime.UtcNow
            };
            _db.AbdmInbounds.Add(inbound);

            if (verdict == AbdmInboundVerdict.Accepted && exchange != null)
            {
                exchange.Status = AbdmExchangeStatus.Answered;
                exchange.AnsweredAt = inbound.ReceivedAt;
                exchange.AnsweredByInboundId = inbound.InboundId;
                if (AbdmCallbackInspector.CallbackError(delivery.Body) is { } error)
                {
                    exchange.ErrorCode = error.Code;
                    exchange.ErrorMessage = error.Message is { Length: > 500 } m ? m[..500] : error.Message;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return inbound;
        }

        private static string Truncate(string body) =>
            body.Length > MaxStoredBody ? body[..MaxStoredBody] : body;
    }
}
