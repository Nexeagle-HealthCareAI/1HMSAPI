using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// WhatsApp-OTP login for the public "Doctor Dekho" booking portal. Deliberately NOT the same
    /// identity space as AuthServicesController's auth/otp/* (that's hospital-staff login, keyed
    /// to a Users row) — a patient isn't staff, and the JWT this issues is only ever validated
    /// manually via IPatientTokenValidator (see GetMyAppointments in PublicController), never
    /// through the app's standard [Authorize] pipeline, so it can never be mistaken for a staff
    /// session on some other controller.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public/patient-auth")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [EnableRateLimiting("PatientAuthPolicy")]
    public class PatientAuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IPatientTokenValidator _tokenValidator;
        private readonly ILogger<PatientAuthController> _logger;

        public PatientAuthController(IMediator mediator, IPatientTokenValidator tokenValidator, ILogger<PatientAuthController> logger)
        {
            _mediator = mediator;
            _tokenValidator = tokenValidator;
            _logger = logger;
        }

        [HttpPost("otp/send")]
        public async Task<ActionResult<PatientOtpSendResponseModel>> SendOtp([FromBody] PatientOtpSendRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PatientAuthController.SendOtp");
                return StatusCode(500, new { Message = "An error occurred while sending the OTP." });
            }
        }

        [HttpPost("otp/verify")]
        public async Task<ActionResult<PatientOtpVerifyResponseModel>> VerifyOtp([FromBody] PatientOtpVerifyRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PatientAuthController.VerifyOtp");
                return StatusCode(500, new { Message = "An error occurred while verifying the OTP." });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var result = await _tokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!result.IsValid || result.Mobile == null)
            {
                // Already logged out / invalid token — logout is idempotent either way.
                return Ok(new PatientLogoutResponseModel { Success = true });
            }

            var response = await _mediator.Send(new PatientLogoutRequestModel { Mobile = result.Mobile });
            return Ok(response);
        }
    }
}
