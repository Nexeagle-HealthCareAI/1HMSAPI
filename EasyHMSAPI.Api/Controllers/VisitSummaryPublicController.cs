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
    /// Fully anonymous "view/deliver e-prescription" link for the structured EPrescriptionPad
    /// flow (Appointment.PdfUrl) — distinct from PrescriptionAttachmentPublicController, which
    /// backs InkRx/manual uploads instead. Keyed only by AppointmentId (already an accepted
    /// anonymous lookup key elsewhere in this controller family, e.g. PublicController's guest
    /// booking lookup). Rate-limited per-IP as defense in depth, same posture as its siblings.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public-visit-summary")]
    [AllowAnonymous]
    [EnableRateLimiting("PerIpPolicy")]
    public class VisitSummaryPublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<VisitSummaryPublicController> _logger;

        public VisitSummaryPublicController(IMediator mediator, ILogger<VisitSummaryPublicController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{appointmentId}")]
        public async Task<ActionResult> View(Guid appointmentId)
        {
            if (appointmentId == Guid.Empty)
                return NotFound();

            try
            {
                var response = await _mediator.Send(new GetPublicVisitSummaryRequestModel { AppointmentId = appointmentId });
                if (!response.Success || string.IsNullOrEmpty(response.RedirectUrl))
                    return NotFound(new { response.Message });

                return Redirect(response.RedirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VisitSummaryPublicController.View");
                return StatusCode(500, new { Message = "An error occurred while loading the prescription." });
            }
        }
    }
}
