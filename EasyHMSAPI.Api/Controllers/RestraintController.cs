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
    // Physical/chemical restraint orders — NABH physician-order + monitoring-interval tracking.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("restraint")]
    [Authorize]
    public class RestraintController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<RestraintController> _logger;

        public RestraintController(IMediator mediator, ILogger<RestraintController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetRestraintOrdersResponseModel>> GetOrders([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetRestraintOrdersRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrders for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading restraint orders." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<StartRestraintResponseModel>> Start([FromBody] StartRestraintRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in Start for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while starting the restraint." });
            }
        }

        [HttpPost("release")]
        public async Task<ActionResult<ReleaseRestraintResponseModel>> Release([FromBody] ReleaseRestraintRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.RestraintOrderId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and restraintOrderId are required." });

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
                _logger.LogError(ex, "Error in Release for restraintOrderId: {RestraintOrderId}", request.RestraintOrderId);
                return StatusCode(500, new { Message = "An error occurred while releasing the restraint." });
            }
        }
    }
}
