using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>The hospital the current request is working for, so a call made deep inside an ABDM
    /// service can be written to that hospital's activity log.</summary>
    public sealed class AbdmCallScope
    {
        public Guid? HospitalId { get; set; }
    }

    /// <summary>Notes the HospitalId of whatever request is being handled. Requests without one are
    /// left alone.</summary>
    public sealed class AbdmHospitalScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        private static readonly PropertyInfo? HospitalIdProperty = typeof(TRequest).GetProperty("HospitalId", BindingFlags.Public | BindingFlags.Instance);
        private readonly AbdmCallScope _scope;

        public AbdmHospitalScopeBehavior(AbdmCallScope scope)
        {
            _scope = scope;
        }

        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (HospitalIdProperty?.GetValue(request) is Guid hospitalId && hospitalId != Guid.Empty)
                _scope.HospitalId = hospitalId;
            return next();
        }
    }

    public interface IAbdmM1CallLog
    {
        /// <summary>Writes one finished ABHA (M1) call to the activity log. Never throws: a log that
        /// cannot be written must not fail the call it describes.</summary>
        Task RecordAsync(Guid? hospitalId, string? requestId, string? path, int? httpStatus, string? errorCode, string? errorMessage, DateTime sentAtUtc, CancellationToken cancellationToken);
    }

    /// <summary>ABHA (M1) calls in the same activity log as the record-sharing calls, so every test
    /// case can be traced by its REQUEST-ID. Only the path, the status and ABDM's error are kept:
    /// never a body, an Aadhaar number, an OTP or a token.</summary>
    public sealed class AbdmM1CallLog : IAbdmM1CallLog
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AbdmM1CallLog> _logger;

        public AbdmM1CallLog(IServiceScopeFactory scopes, ILogger<AbdmM1CallLog> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public async Task RecordAsync(Guid? hospitalId, string? requestId, string? path, int? httpStatus, string? errorCode, string? errorMessage, DateTime sentAtUtc, CancellationToken cancellationToken)
        {
            try
            {
                // Its own scope and DbContext: the row is saved whatever the caller's unit of work does.
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var refused = errorCode != null || errorMessage != null || httpStatus is >= 400;
                var now = DateTime.UtcNow;
                db.AbdmExchanges.Add(new AbdmExchange
                {
                    ExchangeId = Guid.NewGuid(),
                    RequestId = string.IsNullOrWhiteSpace(requestId) ? "local-" + Guid.NewGuid() : requestId,
                    HospitalId = hospitalId,
                    Kind = AbdmM1Calls.Name(path),
                    Path = Cut(path ?? string.Empty, 300),
                    ExpectsCallback = false,
                    Status = refused ? AbdmExchangeStatus.Refused : AbdmExchangeStatus.Done,
                    HttpStatus = httpStatus,
                    ErrorCode = errorCode == null ? null : Cut(errorCode, 50),
                    ErrorMessage = errorMessage == null ? null : Cut(errorMessage, 500),
                    SentAt = sentAtUtc,
                    AcceptedAt = refused ? null : now
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ABDM M1 call with REQUEST-ID {RequestId} could not be written to the activity log.", requestId);
            }
        }

        private static string Cut(string value, int max) => value.Length > max ? value[..max] : value;
    }

    /// <summary>Plain names for the ABHA (M1) calls, as the activity log shows them.</summary>
    public static class AbdmM1Calls
    {
        private static readonly (string Suffix, string Name)[] Names =
        {
            ("/enrollment/request/otp", "ABHA create: send OTP"),
            ("/enrollment/enrol/byAadhaar", "ABHA create: verify Aadhaar OTP"),
            ("/enrollment/auth/byAbdm", "ABHA create: verify mobile OTP"),
            ("/enrollment/enrol/suggestion", "ABHA create: address suggestions"),
            ("/enrollment/enrol/abha-address", "ABHA create: set address"),
            ("/profile/login/request/otp", "ABHA verify: send OTP"),
            ("/profile/login/verify", "ABHA verify: check OTP"),
            ("/profile/login/verify/user", "ABHA verify: choose account"),
            ("/phr/web/login/abha/search", "ABHA address: search"),
            ("/phr/web/login/abha/request/otp", "ABHA address: send OTP"),
            ("/phr/web/login/abha/verify", "ABHA address: check OTP"),
            ("/phr/web/login/profile/abha-profile", "ABHA address: profile"),
            ("/profile/account/abha/search", "ABHA search"),
            ("/profile/account/abha-card", "ABHA card"),
            ("/profile/account/qrCode", "ABHA QR code"),
            ("/profile/account/request/otp", "ABHA profile: send OTP"),
            ("/profile/account/verify", "ABHA profile: check OTP"),
            ("/profile/account", "ABHA profile")
        };

        public static string Name(string? path)
        {
            var clean = (path ?? string.Empty).TrimEnd('/');
            foreach (var (suffix, name) in Names)
            {
                if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            var tail = clean.Length > 50 ? clean[^50..] : clean;
            return ("ABHA " + tail).Trim();
        }
    }
}
