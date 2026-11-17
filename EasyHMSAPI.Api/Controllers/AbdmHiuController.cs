using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Api.Controllers
{
    public sealed class AbdmHiuConsentRequestBody
    {
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string PurposeCode { get; set; } = "CAREMGT";
        public List<string> HiTypes { get; set; } = new();
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime EraseAt { get; set; }
    }

    /// <summary>
    /// ABDM M3 for staff: ask a patient for consent to see their records from other facilities,
    /// follow the request, and read what arrived. Every action checks that the caller works at the
    /// hospital named, because these are records this hospital did not create.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("abdm/hiu")]
    [Authorize]
    [RequiresPermission("abdm")]
    public class AbdmHiuController : ControllerBase
    {
        private readonly IAbdmHiuService _hiu;
        private readonly ILogger<AbdmHiuController> _logger;

        public AbdmHiuController(IAbdmHiuService hiu, ILogger<AbdmHiuController> logger)
        {
            _hiu = hiu;
            _logger = logger;
        }

        private async Task<bool> AllowedAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User) ?? Guid.Empty;
            return hospitalId != Guid.Empty && userId != Guid.Empty && await _hiu.CanAccessAsync(userId, hospitalId, cancellationToken);
        }

        private static object ConsentDto(EasyHMSAPI.Domain.Entities.AbdmConsent c) => new
        {
            consentRowId = c.ConsentRowId,
            consentId = c.ConsentId,
            consentRequestId = c.ConsentRequestId,
            status = c.Status,
            patientId = c.PatientId,
            patientAbhaAddress = c.PatientAbhaAddress,
            fromFacility = c.Status == EasyHMSAPI.Domain.Entities.AbdmConsentStatus.Failed ? null : c.CounterpartyName ?? c.CounterpartyId,
            failureReason = c.Status == EasyHMSAPI.Domain.Entities.AbdmConsentStatus.Failed ? c.CounterpartyName : null,
            purposeCode = c.PurposeCode,
            purposeText = c.PurposeText,
            hiTypes = c.HiTypes.Split(',', StringSplitOptions.RemoveEmptyEntries),
            dateFrom = c.DateFrom,
            dateTo = c.DateTo,
            expiresAt = c.ExpiresAt,
            createdAt = c.CreatedAt,
            updatedAt = c.UpdatedAt
        };

        [HttpPost("consents")]
        public async Task<IActionResult> RequestConsent([FromBody] AbdmHiuConsentRequestBody request, CancellationToken cancellationToken)
        {
            if (!await AllowedAsync(request.HospitalId, cancellationToken)) return Forbid();
            try
            {
                var consent = await _hiu.RequestConsentAsync(new AbdmHiuConsentAsk
                {
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    AbhaAddress = request.AbhaAddress,
                    PurposeCode = request.PurposeCode,
                    HiTypes = request.HiTypes,
                    From = request.From.ToUniversalTime(),
                    To = request.To.ToUniversalTime(),
                    EraseAt = request.EraseAt.ToUniversalTime(),
                    RequesterName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext)
                }, cancellationToken);
                return Ok(new { success = true, consent = ConsentDto(consent) });
            }
            catch (AbdmHiuRefusedException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting ABDM consent for hospital {HospitalId}.", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while asking for consent." });
            }
        }

        [HttpGet("consents")]
        public async Task<IActionResult> GetConsents([FromQuery] Guid hospitalId, [FromQuery] string? patientId, CancellationToken cancellationToken)
        {
            if (!await AllowedAsync(hospitalId, cancellationToken)) return Forbid();
            var consents = await _hiu.ListConsentsAsync(hospitalId, patientId, cancellationToken);
            return Ok(new { success = true, consents = consents.Select(ConsentDto).ToList() });
        }

        [HttpGet("records")]
        public async Task<IActionResult> GetRecords([FromQuery] Guid hospitalId, [FromQuery] string patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId)) return BadRequest(new { Message = "patientId is required." });
            if (!await AllowedAsync(hospitalId, cancellationToken)) return Forbid();
            var records = await _hiu.ListRecordsAsync(hospitalId, patientId, cancellationToken);
            return Ok(new { success = true, records });
        }

        [HttpGet("records/{receivedRecordId:guid}")]
        public async Task<IActionResult> GetRecord(Guid receivedRecordId, [FromQuery] Guid hospitalId, CancellationToken cancellationToken)
        {
            if (!await AllowedAsync(hospitalId, cancellationToken)) return Forbid();
            var record = await _hiu.GetRecordAsync(hospitalId, receivedRecordId, cancellationToken);
            if (record == null) return NotFound();
            // The record's content is never logged; the read itself is.
            _logger.LogInformation("ABDM received record {RecordId} was opened by user {UserId}.", receivedRecordId, UserContextHelper.GetUserId(HttpContext.User));
            return Ok(new
            {
                success = true,
                receivedRecordId = record.ReceivedRecordId,
                title = record.Title,
                hiType = record.HiType,
                receivedAt = record.ReceivedAt,
                eraseAt = record.EraseAt,
                bundle = JsonSerializer.Deserialize<JsonElement>(record.BundleJson)
            });
        }
    }

    /// <summary>
    /// Where another facility sends the encrypted records we asked for (ABDM M3). The address is
    /// made for one transfer: its last segment is a random token that stops working once the
    /// records are in. Anything else gets a plain 404.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [AllowAnonymous]
    [Route("abdm-hiu/data-push")]
    public class AbdmHiuDataController : ControllerBase
    {
        // Several FHIR bundles, encrypted and base64 encoded.
        private const int MaxBodyBytes = 25 * 1024 * 1024;
        private readonly IAbdmHiuService _hiu;
        private readonly ILogger<AbdmHiuDataController> _logger;

        public AbdmHiuDataController(IAbdmHiuService hiu, ILogger<AbdmHiuDataController> logger)
        {
            _hiu = hiu;
            _logger = logger;
        }

        [HttpPost("{token}")]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Receive(string token, CancellationToken cancellationToken)
        {
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken);
            try
            {
                return await _hiu.ReceiveDataAsync(token, body, cancellationToken) ? Accepted() : NotFound();
            }
            catch (Exception ex)
            {
                // Never the body: it is somebody's health record.
                _logger.LogError("Receiving an ABDM data push failed: {Error}.", ex.GetType().Name);
                return StatusCode(500);
            }
        }
    }
}
