using System;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Fully anonymous "scan the QR on a pathology report" endpoint -- no staff JWT, no API key.
    /// ReportId alone confirms the report is real and approved; an optional ?hash= query param
    /// (typed in from the "Document Hash" line printed separately on the report) upgrades the
    /// check to a strict match against the uploaded PDF's bytes -- see
    /// GetPathologyReportVerificationHandler.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("verify/report")]
    [AllowAnonymous]
    [EnableRateLimiting("PerIpPolicy")]
    public class PathologyReportVerificationPublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PathologyReportVerificationPublicController> _logger;

        public PathologyReportVerificationPublicController(IMediator mediator, ILogger<PathologyReportVerificationPublicController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{reportId}")]
        public async Task<IActionResult> Verify(Guid reportId, [FromQuery] string? hash)
        {
            try
            {
                var response = await _mediator.Send(new GetPathologyReportVerificationQuery { ReportId = reportId, Sha256 = hash });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying pathology report {ReportId}", reportId);
                return StatusCode(500, new { Message = "An error occurred while verifying this report." });
            }
        }
    }
}
