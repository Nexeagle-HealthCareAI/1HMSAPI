using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Health Wiki lookups for the CMS API (server-to-server, shared key): the conditions an article can link to
    /// and the counts on the CMS tabs. Contributors and topic requests are added here as they are built.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("internal/health-wiki")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [ServiceFilter(typeof(InternalApiKeyFilter))]
    [EnableRateLimiting("PerIpPolicy")]
    public class HealthWikiInternalController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<HealthWikiInternalController> _logger;

        public HealthWikiInternalController(IMediator mediator, ILogger<HealthWikiInternalController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("conditions")]
        public async Task<IActionResult> Conditions()
        {
            try
            {
                return Ok((await _mediator.Send(new GetHealthWikiConditionsRequestModel())).Conditions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Health Wiki conditions");
                return StatusCode(500, new { message = "An error occurred while reading the conditions." });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            try
            {
                return Ok(await _mediator.Send(new GetHealthWikiSummaryRequestModel()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Health Wiki summary");
                return StatusCode(500, new { message = "An error occurred while reading the summary." });
            }
        }
    }
}
