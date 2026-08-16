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
    // ICU — level-of-care tracking, APACHE II and SOFA critical-care scoring (APACHE/SOFA routes
    // added alongside their own handlers).
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("icu")]
    [Authorize]
    [RequiresPermission("icu_board")]
    public class IcuController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<IcuController> _logger;

        public IcuController(IMediator mediator, ILogger<IcuController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("board")]
        public async Task<ActionResult<GetIcuBoardResponseModel>> GetIcuBoard([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetIcuBoardRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetIcuBoard for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the ICU board." });
            }
        }

        [HttpPost("level-of-care")]
        public async Task<ActionResult<RecordLevelOfCareResponseModel>> RecordLevelOfCare([FromBody] RecordLevelOfCareRequestModel request)
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
                _logger.LogError(ex, "Error in RecordLevelOfCare for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the level of care." });
            }
        }

        [HttpGet("level-of-care/history")]
        public async Task<ActionResult<GetLevelOfCareHistoryResponseModel>> GetLevelOfCareHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetLevelOfCareHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLevelOfCareHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching level of care history." });
            }
        }

        [HttpGet("apache/autofill")]
        public async Task<ActionResult<GetApacheIIAutoFillResponseModel>> GetApacheAutoFill([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetApacheIIAutoFillRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetApacheAutoFill for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while composing the APACHE II auto-fill." });
            }
        }

        [HttpPost("apache")]
        public async Task<ActionResult<RecordApacheIIScoreResponseModel>> RecordApacheScore([FromBody] RecordApacheIIScoreRequestModel request)
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
                _logger.LogError(ex, "Error in RecordApacheScore for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the APACHE II score." });
            }
        }

        [HttpGet("apache/history")]
        public async Task<ActionResult<GetApacheIIScoreHistoryResponseModel>> GetApacheHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetApacheIIScoreHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetApacheHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching APACHE II score history." });
            }
        }

        [HttpGet("sofa/autofill")]
        public async Task<ActionResult<GetSofaAutoFillResponseModel>> GetSofaAutoFill([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetSofaAutoFillRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSofaAutoFill for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while composing the SOFA auto-fill." });
            }
        }

        [HttpPost("sofa")]
        public async Task<ActionResult<RecordSofaScoreResponseModel>> RecordSofaScore([FromBody] RecordSofaScoreRequestModel request)
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
                _logger.LogError(ex, "Error in RecordSofaScore for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the SOFA score." });
            }
        }

        [HttpGet("sofa/history")]
        public async Task<ActionResult<GetSofaScoreHistoryResponseModel>> GetSofaHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetSofaScoreHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSofaHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching SOFA score history." });
            }
        }

        [HttpPost("ventilator")]
        public async Task<ActionResult<RecordVentilatorSettingsResponseModel>> RecordVentilatorSettings([FromBody] RecordVentilatorSettingsRequestModel request)
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
                _logger.LogError(ex, "Error in RecordVentilatorSettings for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording ventilator settings." });
            }
        }

        [HttpGet("ventilator/history")]
        public async Task<ActionResult<GetVentilatorSettingsHistoryResponseModel>> GetVentilatorHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetVentilatorSettingsHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetVentilatorHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching ventilator settings history." });
            }
        }

        [HttpPost("weaning")]
        public async Task<ActionResult<RecordWeaningAssessmentResponseModel>> RecordWeaningAssessment([FromBody] RecordWeaningAssessmentRequestModel request)
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
                _logger.LogError(ex, "Error in RecordWeaningAssessment for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the weaning assessment." });
            }
        }

        [HttpGet("weaning/history")]
        public async Task<ActionResult<GetWeaningAssessmentHistoryResponseModel>> GetWeaningHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetWeaningAssessmentHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetWeaningHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching weaning assessment history." });
            }
        }
    }
}
