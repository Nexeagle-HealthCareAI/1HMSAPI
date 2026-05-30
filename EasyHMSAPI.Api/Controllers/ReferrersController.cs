using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("referrers")]
    public class ReferrersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ReferrersController> _logger;
        public ReferrersController(IMediator mediator, ILogger<ReferrersController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetReferrers([FromQuery] Guid hospitalId, [FromQuery] bool activeOnly = true, [FromQuery] string? search = null)
        {
            _logger.LogInformation("GetReferrers started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            var request = new GetReferrersRequestModel { HospitalId = hospitalId, ActiveOnly = activeOnly, Search = search };
            var response = await _mediator.Send(request);
            _logger.LogInformation("GetReferrers ended for hospitalId: {HospitalId}", hospitalId);

            return Ok(response);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReferrer([FromQuery] Guid hospitalId, [FromBody] CreateReferrerRequestModel request)
        {
            _logger.LogInformation("CreateReferrer started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (string.IsNullOrWhiteSpace(request.ReferrerName))
                return BadRequest(new { Message = "Referrer name is required." });

            var userIdClaim = User.FindFirst("userId")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
                request.UserId = userId;

            try
            {
                request.HospitalId = hospitalId;
                var response = await _mediator.Send(request);
                _logger.LogInformation("CreateReferrer successful for hospitalId: {HospitalId}, ReferrerId: {ReferrerId}", hospitalId, response.ReferrerId);
                return Ok(response);
            }
            catch (ArgumentException aex)
            {
                return BadRequest(new { aex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateReferrer for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { ex.Message });
            }
        }

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateReferrer([FromQuery] Guid hospitalId, [FromBody] UpdateReferrerRequestModel request)
        {
            _logger.LogInformation("UpdateReferrer started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (request.ReferrerId == Guid.Empty)
                return BadRequest(new { Message = "referrerId is required." });
            if (string.IsNullOrWhiteSpace(request.ReferrerName))
                return BadRequest(new { Message = "Referrer name is required." });

            var userIdClaim = User.FindFirst("userId")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
                request.UserId = userId;

            try
            {
                request.HospitalId = hospitalId;
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return NotFound(new { response.Message });
                _logger.LogInformation("UpdateReferrer successful for ReferrerId: {ReferrerId}", response.ReferrerId);
                return Ok(response);
            }
            catch (ArgumentException aex)
            {
                return BadRequest(new { aex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateReferrer for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { ex.Message });
            }
        }
    }
}
