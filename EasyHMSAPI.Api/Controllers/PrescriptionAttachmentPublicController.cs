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
    /// Fully anonymous "view/deliver prescription" link — no staff JWT, no API key, keyed only
    /// by AttachmentId (an unguessable GUID, never any other identifier). This is what the
    /// printed QR code and the WhatsApp bot's GET /rx/{id} both point at. Rate-limited per-IP as
    /// defense in depth even though the id itself is unguessable, mirroring
    /// DischargeSummaryPublicController's own posture.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public-prescription")]
    [AllowAnonymous]
    [EnableRateLimiting("PerIpPolicy")]
    public class PrescriptionAttachmentPublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PrescriptionAttachmentPublicController> _logger;

        public PrescriptionAttachmentPublicController(IMediator mediator, ILogger<PrescriptionAttachmentPublicController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{attachmentId}")]
        public async Task<ActionResult> View(Guid attachmentId)
        {
            if (attachmentId == Guid.Empty)
                return NotFound();

            try
            {
                var response = await _mediator.Send(new GetPublicPrescriptionAttachmentRequestModel { AttachmentId = attachmentId });
                if (!response.Success || string.IsNullOrEmpty(response.RedirectUrl))
                    return NotFound(new { response.Message });

                return Redirect(response.RedirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PrescriptionAttachmentPublicController.View");
                return StatusCode(500, new { Message = "An error occurred while loading the prescription." });
            }
        }
    }
}
