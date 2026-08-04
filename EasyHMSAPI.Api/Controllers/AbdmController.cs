using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// ABDM M1 milestone: ABHA creation (Aadhaar-OTP) and existing-ABHA login/linking
    /// (Mobile/Aadhaar-OTP). See EasyHMSAPI.Application.Services.Implementations.AbdmAbhaService for
    /// the ABDM V3 call sequence.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("abdm")]
    [Authorize]
    public class AbdmController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AbdmController> _logger;

        public AbdmController(IMediator mediator, ILogger<AbdmController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("accounts")]
        public async Task<ActionResult<GetAbhaAccountsResponseModel>> GetAccounts([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetAbhaAccountsRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ABHA accounts for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching ABHA accounts." });
            }
        }

        [HttpPost("aadhaar/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> GenerateAadhaarOtp([FromBody] GenerateAadhaarOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ABHA Aadhaar OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }

        [HttpPost("aadhaar/verify-otp")]
        public async Task<ActionResult<AbdmEnrollResponseModel>> VerifyAadhaarOtp([FromBody] VerifyAadhaarOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ABHA Aadhaar OTP.");
                return StatusCode(500, new { Message = "An error occurred while verifying the OTP." });
            }
        }

        [HttpPost("mobile/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> GenerateMobileOtp([FromBody] GenerateAbdmMobileOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ABHA mobile OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }

        [HttpPost("mobile/verify-otp")]
        public async Task<ActionResult<AbdmEnrollResponseModel>> VerifyMobileOtp([FromBody] VerifyAbdmMobileOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ABHA mobile OTP.");
                return StatusCode(500, new { Message = "An error occurred while verifying the OTP." });
            }
        }

        [HttpGet("abha-address/suggestions")]
        public async Task<ActionResult<AbdmAddressSuggestionsResponseModel>> GetAbhaAddressSuggestions([FromQuery] string txnId)
        {
            try
            {
                var response = await _mediator.Send(new GetAbhaAddressSuggestionsRequestModel { TxnId = txnId });
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ABHA address suggestions.");
                return StatusCode(500, new { Message = "An error occurred while fetching ABHA address suggestions." });
            }
        }

        [HttpPost("abha-address")]
        public async Task<ActionResult<AbdmEnrollResponseModel>> CreateAbhaAddress([FromBody] CreateAbhaAddressRequestModel request)
        {
            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ABHA address.");
                return StatusCode(500, new { Message = "An error occurred while creating the ABHA address." });
            }
        }

        [HttpPost("login/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> RequestLoginOtp([FromBody] RequestAbdmLoginOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting ABHA login OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }

        [HttpPost("login/verify-otp")]
        public async Task<ActionResult<AbdmProfileResponseModel>> VerifyLoginOtp([FromBody] VerifyAbdmLoginOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ABHA login OTP.");
                return StatusCode(500, new { Message = "An error occurred while verifying the OTP." });
            }
        }

        [HttpPost("accounts/link")]
        public async Task<ActionResult<SaveAbhaAccountResponseModel>> SaveLinkedAccount([FromBody] SaveLinkedAbhaAccountRequestModel request)
        {
            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving linked ABHA account.");
                return StatusCode(500, new { Message = "An error occurred while saving the ABHA account." });
            }
        }

        // ---- Edit profile (re-verify via OTP first, then update mobile/email) ----

        [HttpPost("profile/mobile/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> RequestUpdateMobileOtp([FromBody] RequestUpdateMobileOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting ABHA mobile-update OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }

        [HttpPost("profile/mobile/verify-otp")]
        public async Task<ActionResult<AbdmUpdateResponseModel>> VerifyUpdateMobileOtp([FromBody] VerifyUpdateMobileOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ABHA mobile-update OTP.");
                return StatusCode(500, new { Message = "An error occurred while updating the mobile number." });
            }
        }

        [HttpPost("profile/email")]
        public async Task<ActionResult<AbdmUpdateResponseModel>> UpdateEmail([FromBody] UpdateAbhaEmailRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ABHA email.");
                return StatusCode(500, new { Message = "An error occurred while updating the email." });
            }
        }

        // ---- Read-only ABDM-side artifacts (§9/§10/§11) — all require the same live,
        // freshly-OTP-verified sessionTxnId as the profile-update endpoints above. ----

        [HttpGet("profile")]
        public async Task<ActionResult<AbdmProfileResponseModel>> GetProfile([FromQuery] Guid hospitalId, [FromQuery] string sessionTxnId)
        {
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
            try
            {
                var response = await _mediator.Send(new GetAbdmProfileRequestModel { HospitalId = hospitalId, SessionTxnId = sessionTxnId });
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching live ABHA profile.");
                return StatusCode(500, new { Message = "An error occurred while fetching the profile." });
            }
        }

        [HttpGet("profile/qr-code")]
        public async Task<IActionResult> GetQrCode([FromQuery] Guid hospitalId, [FromQuery] string sessionTxnId)
        {
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
            try
            {
                var response = await _mediator.Send(new GetAbdmQrCodeRequestModel { HospitalId = hospitalId, SessionTxnId = sessionTxnId });
                if (!response.Success || response.Content == null) return BadRequest(new { response.Message });
                return File(response.Content, response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ABHA QR code.");
                return StatusCode(500, new { Message = "An error occurred while fetching the QR code." });
            }
        }

        [HttpGet("profile/abha-card")]
        public async Task<IActionResult> GetAbhaCard([FromQuery] Guid hospitalId, [FromQuery] string sessionTxnId)
        {
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
            try
            {
                var response = await _mediator.Send(new GetAbdmAbhaCardRequestModel { HospitalId = hospitalId, SessionTxnId = sessionTxnId });
                if (!response.Success || response.Content == null) return BadRequest(new { response.Message });
                return File(response.Content, response.ContentType, "abha-card");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ABHA card.");
                return StatusCode(500, new { Message = "An error occurred while fetching the ABHA card." });
            }
        }

        // ---- §7.6 Find ABHA — for a holder who has a mobile/Aadhaar but doesn't remember their
        // exact ABHA number/address. ----

        [HttpPost("find/search")]
        public async Task<ActionResult<AbdmFindAbhaSearchResponseModel>> FindAbhaSearch([FromBody] FindAbhaSearchRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for ABHA.");
                return StatusCode(500, new { Message = "An error occurred while searching for the ABHA number." });
            }
        }

        [HttpPost("find/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> FindAbhaGenerateOtp([FromBody] FindAbhaGenerateOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Find-ABHA OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }
        // Verification reuses POST abdm/login/verify-otp — same endpoint/response shape as a normal login.

        // ---- §8.4/§8.5 Deactivate / Re-activate ABHA ----

        [HttpPost("profile/deactivate/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> RequestDeactivateOtp([FromBody] RequestDeactivateAbhaOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting ABHA deactivation OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }

        [HttpPost("profile/deactivate/verify-otp")]
        public async Task<ActionResult<AbdmUpdateResponseModel>> VerifyDeactivateOtp([FromBody] VerifyDeactivateAbhaOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating ABHA.");
                return StatusCode(500, new { Message = "An error occurred while deactivating the ABHA number." });
            }
        }

        [HttpPost("profile/reactivate/generate-otp")]
        public async Task<ActionResult<AbdmOtpTxnResponseModel>> RequestReactivateOtp([FromBody] RequestReactivateAbhaOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting ABHA reactivation OTP.");
                return StatusCode(500, new { Message = "An error occurred while requesting the OTP." });
            }
        }

        [HttpPost("profile/reactivate/verify-otp")]
        public async Task<ActionResult<AbdmProfileResponseModel>> VerifyReactivateOtp([FromBody] VerifyReactivateAbhaOtpRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reactivating ABHA.");
                return StatusCode(500, new { Message = "An error occurred while reactivating the ABHA number." });
            }
        }
    }
}
