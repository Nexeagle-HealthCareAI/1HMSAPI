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
    // CSSD — instrument set/tray master, movement loop, sterilization cycle log.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("cssd")]
    [Authorize]
    public class CssdController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CssdController> _logger;

        public CssdController(IMediator mediator, ILogger<CssdController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("sets")]
        public async Task<ActionResult<GetInstrumentSetsResponseModel>> GetSets([FromQuery] Guid hospitalId, [FromQuery] string? status)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetInstrumentSetsRequestModel { HospitalId = hospitalId, Status = status });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSets for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching instrument sets." });
            }
        }

        [HttpPost("set")]
        public async Task<ActionResult<CreateInstrumentSetResponseModel>> CreateSet([FromBody] CreateInstrumentSetRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateSet for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the instrument set." });
            }
        }

        [HttpPost("set/movement")]
        public async Task<ActionResult<RecordInstrumentSetMovementResponseModel>> RecordMovement([FromBody] RecordInstrumentSetMovementRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.InstrumentSetId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and instrumentSetId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RecordMovement for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the movement." });
            }
        }

        [HttpGet("sterilization-cycle/history")]
        public async Task<ActionResult<GetSterilizationCycleHistoryResponseModel>> GetCycleHistory([FromQuery] Guid hospitalId, [FromQuery] int take = 50)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetSterilizationCycleHistoryRequestModel { HospitalId = hospitalId, Take = take });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCycleHistory for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching sterilization cycle history." });
            }
        }

        [HttpPost("sterilization-cycle")]
        public async Task<ActionResult<RecordSterilizationCycleResponseModel>> RecordCycle([FromBody] RecordSterilizationCycleRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RecordCycle for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the sterilization cycle." });
            }
        }
    }
}
