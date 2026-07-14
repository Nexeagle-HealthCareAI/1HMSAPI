using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Fully anonymous "view discharge summary on mobile" link — no staff JWT, no API key, keyed
    /// only by a long random AccessToken (never the AdmissionId). This is what the printed QR code
    /// and the WhatsApp-shared link both point at. Rate-limited per-IP as defense in depth even
    /// though the token itself is unguessable.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public-discharge")]
    [AllowAnonymous]
    [EnableRateLimiting("PerIpPolicy")]
    public class DischargeSummaryPublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DischargeSummaryPublicController> _logger;

        public DischargeSummaryPublicController(IMediator mediator, ILogger<DischargeSummaryPublicController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{accessToken}")]
        public async Task<ActionResult> View(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return NotFound();

            try
            {
                var response = await _mediator.Send(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = accessToken });
                if (!response.Success || string.IsNullOrEmpty(response.RedirectUrl))
                    return NotFound(new { response.Message });

                return Redirect(response.RedirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DischargeSummaryPublicController.View");
                return StatusCode(500, new { Message = "An error occurred while loading the discharge summary." });
            }
        }
    }
}
