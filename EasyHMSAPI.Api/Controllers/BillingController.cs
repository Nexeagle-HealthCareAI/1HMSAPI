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
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("billing")]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<BillingController> _logger;

        public BillingController(IMediator mediator, ILogger<BillingController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("policy")]
        public async Task<ActionResult<GetBillingPolicyResponseModel>> GetBillingPolicy([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetBillingPolicyRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBillingPolicy for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching billing policy." });
            }
        }

        [HttpPut("policy")]
        public async Task<ActionResult<UpsertBillingPolicyResponseModel>> UpdateBillingPolicy([FromBody] UpsertBillingPolicyRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "Invalid request data", Errors = ModelState.Values.SelectMany(v => v.Errors) });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateBillingPolicy for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating billing policy." });
            }
        }
    }
}
