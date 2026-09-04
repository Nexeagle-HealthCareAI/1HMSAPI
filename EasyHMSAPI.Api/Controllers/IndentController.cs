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
    // Department stock requests — the first stage of the procurement backbone (Indent -> PO -> GRN).
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory/indents")]
    [Authorize]
    [RequiresPermission("inventory", "pharmacy")]
    public class IndentController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<IndentController> _logger;

        public IndentController(IMediator mediator, ILogger<IndentController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetIndentsResponseModel>> GetIndents([FromQuery] Guid hospitalId, [FromQuery] string? status)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetIndentsRequestModel { HospitalId = hospitalId, Status = status });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetIndents for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching indents." });
            }
        }

        [HttpGet("{indentId:guid}")]
        public async Task<ActionResult<GetIndentDetailResponseModel>> GetIndentDetail(Guid indentId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || indentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and indentId are required." });

            try
            {
                var response = await _mediator.Send(new GetIndentDetailRequestModel { HospitalId = hospitalId, IndentId = indentId });
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetIndentDetail for indentId: {IndentId}", indentId);
                return StatusCode(500, new { Message = "An error occurred while fetching the indent." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<CreateIndentResponseModel>> CreateIndent([FromBody] CreateIndentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateIndent for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the indent." });
            }
        }

        [HttpPost("{indentId:guid}/submit")]
        public async Task<ActionResult<ApproveIndentResponseModel>> SubmitIndent(Guid indentId, [FromBody] SubmitIndentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.IndentId = indentId;

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
                _logger.LogError(ex, "Error in SubmitIndent for indentId: {IndentId}", indentId);
                return StatusCode(500, new { Message = "An error occurred while submitting the indent." });
            }
        }

        [HttpPost("{indentId:guid}/decide")]
        public async Task<ActionResult<ApproveIndentResponseModel>> ApproveIndent(Guid indentId, [FromBody] ApproveIndentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.IndentId = indentId;

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApproveIndent for indentId: {IndentId}", indentId);
                return StatusCode(500, new { Message = "An error occurred while deciding the indent." });
            }
        }

        [HttpPost("{indentId:guid}/convert-to-po")]
        public async Task<ActionResult<ConvertIndentToPoResponseModel>> ConvertToPo(Guid indentId, [FromBody] ConvertIndentToPoRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.IndentId = indentId;

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
                _logger.LogError(ex, "Error in ConvertToPo for indentId: {IndentId}", indentId);
                return StatusCode(500, new { Message = "An error occurred while converting the indent to a purchase order." });
            }
        }

        [HttpPost("{indentId:guid}/issue")]
        public async Task<ActionResult<IssueIndentResponseModel>> IssueIndent(Guid indentId, [FromBody] IssueIndentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.IndentId = indentId;

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IssueIndent for indentId: {IndentId}", indentId);
                return StatusCode(500, new { Message = "An error occurred while issuing the indent." });
            }
        }
    }
}
