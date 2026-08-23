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
    [Route("ot-plan")]
    [Authorize]
    [RequiresPermission("ot_board")]
    public class OtPlanController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OtPlanController> _logger;

        public OtPlanController(IMediator mediator, ILogger<OtPlanController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<ActionResult<GetOTPlansResponseModel>> GetOTPlans([FromQuery] Guid hospitalId, [FromQuery] Guid? departmentId, [FromQuery] bool includeInactive = false)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetOTPlansRequestModel { HospitalId = hospitalId, DepartmentId = departmentId, IncludeInactive = includeInactive };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOTPlans for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("upsert")]
        public async Task<ActionResult<UpsertOTPlanResponseModel>> UpsertOTPlan([FromBody] UpsertOTPlanRequestModel request)
        {
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
                _logger.LogError(ex, "Error in UpsertOTPlan for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
