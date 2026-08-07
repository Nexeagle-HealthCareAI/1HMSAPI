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
    [Route("admission-referral")]
    [Authorize]
    public class AdmissionReferralController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AdmissionReferralController> _logger;

        public AdmissionReferralController(IMediator mediator, ILogger<AdmissionReferralController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("advise")]
        public async Task<ActionResult<AdviseAdmissionResponseModel>> AdviseAdmission([FromBody] AdviseAdmissionRequestModel request)
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
                _logger.LogError(ex, "Error in AdviseAdmission for hospitalId: {HospitalId}, patientId: {PatientId}", request.HospitalId, request.PatientId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("list")]
        public async Task<ActionResult<GetAdmissionReferralsResponseModel>> GetAdmissionReferrals(
            [FromQuery] Guid hospitalId, [FromQuery] string? patientId, [FromQuery] string? statusCode, [FromQuery] string? caseType,
            [FromQuery] Guid? referringDoctorId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetAdmissionReferralsRequestModel
                {
                    HospitalId = hospitalId,
                    PatientId = patientId,
                    StatusCode = statusCode,
                    CaseType = caseType,
                    ReferringDoctorId = referringDoctorId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page < 1 ? 1 : page,
                    PageSize = pageSize < 1 ? 5 : pageSize,
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAdmissionReferrals for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Adds a timestamped, author-attributed comment against a referral row.
        [HttpPost("comment")]
        public async Task<ActionResult<AddAdmissionReferralCommentResponseModel>> AddComment([FromBody] AddAdmissionReferralCommentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.ReferralId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and referralId are required." });

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
                _logger.LogError(ex, "Error in AddComment for referralId: {ReferralId}", request.ReferralId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // All comments for one referral, newest first.
        [HttpGet("comments")]
        public async Task<ActionResult<GetAdmissionReferralCommentsResponseModel>> GetComments([FromQuery] Guid hospitalId, [FromQuery] Guid referralId)
        {
            if (hospitalId == Guid.Empty || referralId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and referralId are required." });

            try
            {
                var request = new GetAdmissionReferralCommentsRequestModel { HospitalId = hospitalId, ReferralId = referralId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetComments for referralId: {ReferralId}", referralId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("details")]
        public async Task<ActionResult<UpdateAdmissionReferralDetailsResponseModel>> UpdateAdmissionReferralDetails([FromBody] UpdateAdmissionReferralDetailsRequestModel request)
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
                _logger.LogError(ex, "Error in UpdateAdmissionReferralDetails for referralId: {ReferralId}", request.ReferralId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("status")]
        public async Task<ActionResult<UpdateAdmissionReferralStatusResponseModel>> UpdateAdmissionReferralStatus([FromBody] UpdateAdmissionReferralStatusRequestModel request)
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
                _logger.LogError(ex, "Error in UpdateAdmissionReferralStatus for referralId: {ReferralId}", request.ReferralId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
