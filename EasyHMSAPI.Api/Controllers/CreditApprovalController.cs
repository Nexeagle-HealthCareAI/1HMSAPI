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
    [Route("credit-approvals")]
    [Authorize]
    [RequiresPermission("billing")]
    public class CreditApprovalController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CreditApprovalController> _logger;

        public CreditApprovalController(IMediator mediator, ILogger<CreditApprovalController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetCreditApprovalsResponseModel>> GetCreditApprovals(
            [FromQuery] Guid hospitalId, [FromQuery] string? status, [FromQuery] Guid? encounterId,
            [FromQuery] string? patientId, [FromQuery] int? take)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetCreditApprovalsRequestModel
                {
                    HospitalId = hospitalId,
                    Status = status,
                    EncounterId = encounterId,
                    PatientId = patientId,
                    Take = take,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCreditApprovals for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while loading credit approvals." });
            }
        }

        [HttpPost("decide")]
        public async Task<ActionResult<DecideCreditApprovalResponseModel>> DecideCreditApproval([FromBody] DecideCreditApprovalRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.CreditApprovalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and creditApprovalId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DecideCreditApproval for creditApprovalId: {CreditApprovalId}", request.CreditApprovalId);
                return StatusCode(500, new { Message = "An error occurred while deciding the credit approval." });
            }
        }
    }
}
