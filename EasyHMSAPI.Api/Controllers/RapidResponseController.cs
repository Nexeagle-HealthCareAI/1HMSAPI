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
    [Route("rapid-response")]
    [Authorize]
    public class RapidResponseController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<RapidResponseController> _logger;

        public RapidResponseController(IMediator mediator, ILogger<RapidResponseController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ActivateRapidResponseResponseModel>> Activate([FromBody] ActivateRapidResponseRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in Activate for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while activating Rapid Response." });
            }
        }

        [HttpPost("arrive")]
        public async Task<ActionResult<UpdateRapidResponseResponseModel>> MarkArrived([FromBody] MarkRapidResponseArrivedRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.ActivationId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and activationId are required." });

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkArrived for activationId: {ActivationId}", request.ActivationId);
                return StatusCode(500, new { Message = "An error occurred while recording arrival." });
            }
        }

        [HttpPost("resolve")]
        public async Task<ActionResult<UpdateRapidResponseResponseModel>> Resolve([FromBody] ResolveRapidResponseRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.ActivationId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and activationId are required." });

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
                _logger.LogError(ex, "Error in Resolve for activationId: {ActivationId}", request.ActivationId);
                return StatusCode(500, new { Message = "An error occurred while resolving Rapid Response." });
            }
        }

        [HttpGet("history")]
        public async Task<ActionResult<GetRapidResponseHistoryResponseModel>> GetHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetRapidResponseHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching Rapid Response history." });
            }
        }

        [HttpGet("open")]
        public async Task<ActionResult<GetOpenRapidResponsesResponseModel>> GetOpen([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetOpenRapidResponsesRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOpen for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching open Rapid Response activations." });
            }
        }
    }
}
