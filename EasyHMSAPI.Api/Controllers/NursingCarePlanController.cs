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
    // Free-text nursing diagnoses/goals/interventions with an ACTIVE/RESOLVED/DISCONTINUED lifecycle.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("nursing-care-plan")]
    [Authorize]
    public class NursingCarePlanController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NursingCarePlanController> _logger;

        public NursingCarePlanController(IMediator mediator, ILogger<NursingCarePlanController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetNursingCarePlanResponseModel>> GetItems([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetNursingCarePlanRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetItems for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading the nursing care plan." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<CreateNursingCarePlanItemResponseModel>> Create([FromBody] CreateNursingCarePlanItemRequestModel request)
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
                _logger.LogError(ex, "Error in Create for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while adding the care plan item." });
            }
        }

        [HttpPost("resolve")]
        public async Task<ActionResult<ResolveNursingCarePlanItemResponseModel>> Resolve([FromBody] ResolveNursingCarePlanItemRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.CarePlanItemId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and carePlanItemId are required." });

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
                _logger.LogError(ex, "Error in Resolve for carePlanItemId: {CarePlanItemId}", request.CarePlanItemId);
                return StatusCode(500, new { Message = "An error occurred while updating the care plan item." });
            }
        }
    }
}
