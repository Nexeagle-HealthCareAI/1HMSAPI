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
    // Surgery case clinical lifecycle: request/status, pre-op assessment, WHO checklist,
    // intra-op record + item usage (added by the OT phase's intra-op module).
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("surgery-case")]
    [Authorize]
    public class SurgeryCaseController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<SurgeryCaseController> _logger;

        public SurgeryCaseController(IMediator mediator, ILogger<SurgeryCaseController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("admission/{admissionId:guid}")]
        public async Task<ActionResult<GetSurgeryCasesForAdmissionResponseModel>> GetForAdmission([FromQuery] Guid hospitalId, Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetSurgeryCasesForAdmissionRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetForAdmission for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching surgery cases." });
            }
        }

        [HttpGet("{surgeryCaseId:guid}")]
        public async Task<ActionResult<GetSurgeryCaseDetailResponseModel>> GetDetail([FromQuery] Guid hospitalId, Guid surgeryCaseId)
        {
            if (hospitalId == Guid.Empty || surgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

            try
            {
                var response = await _mediator.Send(new GetSurgeryCaseDetailRequestModel { HospitalId = hospitalId, SurgeryCaseId = surgeryCaseId });
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDetail for surgeryCaseId: {SurgeryCaseId}", surgeryCaseId);
                return StatusCode(500, new { Message = "An error occurred while fetching the surgery case." });
            }
        }

        [HttpPost("request")]
        public async Task<ActionResult<RequestSurgeryResponseModel>> RequestSurgery([FromBody] RequestSurgeryRequestModel request)
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
                _logger.LogError(ex, "Error in RequestSurgery for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while requesting the surgery." });
            }
        }

        [HttpPost("status")]
        public async Task<ActionResult<UpdateSurgeryCaseStatusResponseModel>> UpdateStatus([FromBody] UpdateSurgeryCaseStatusRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in UpdateStatus for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating the surgery case status." });
            }
        }

        [HttpPost("pre-op")]
        public async Task<ActionResult<RecordPreOpAssessmentResponseModel>> RecordPreOp([FromBody] RecordPreOpAssessmentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in RecordPreOp for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the pre-op assessment." });
            }
        }

        [HttpPost("checklist/sign-in")]
        public async Task<ActionResult<RecordSignInResponseModel>> RecordSignIn([FromBody] RecordSignInRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in RecordSignIn for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording Sign-In." });
            }
        }

        [HttpPost("checklist/time-out")]
        public async Task<ActionResult<RecordTimeOutResponseModel>> RecordTimeOut([FromBody] RecordTimeOutRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in RecordTimeOut for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording Time-Out." });
            }
        }

        [HttpPost("checklist/sign-out")]
        public async Task<ActionResult<RecordSignOutResponseModel>> RecordSignOut([FromBody] RecordSignOutRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in RecordSignOut for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording Sign-Out." });
            }
        }

        [HttpPost("intra-op")]
        public async Task<ActionResult<SaveIntraOpRecordResponseModel>> SaveIntraOp([FromBody] SaveIntraOpRecordRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in SaveIntraOp for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the intra-op record." });
            }
        }

        [HttpPost("item-usage")]
        public async Task<ActionResult<RecordIntraOpItemUsageResponseModel>> RecordItemUsage([FromBody] RecordIntraOpItemUsageRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and surgeryCaseId are required." });

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
                _logger.LogError(ex, "Error in RecordItemUsage for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording item usage." });
            }
        }
    }
}
