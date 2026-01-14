using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using Microsoft.AspNetCore.Authorization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{

    [ExcludeFromCodeCoverage]
    [Route("hospitals")]
    [ApiController]
    public class HospitalsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<HospitalsController> _logger;
        public HospitalsController(IMediator mediator, ILogger<HospitalsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("register")]
        [Authorize]
        public async Task<ActionResult<HospitalRegisterResponseModel>> RegisterHospital([FromBody] HospitalRegisterRequestModel request)
        {
            _logger.LogInformation("RegisterHospital started at {Time}", DateTime.UtcNow);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("RegisterHospital ended");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterHospital");
                return StatusCode(500, new { Message = "An error occurred while registering hospital", Error = ex.Message });
            }
        }

        [HttpPut("{hospitalId}")]
        [Authorize]
        public async Task<ActionResult<HospitalUpdateResponseModel>> UpdateHospital(Guid hospitalId, [FromBody] HospitalUpdateRequestModel request)
        {
            _logger.LogInformation("UpdateHospital started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    return BadRequest("Hospital ID is required and cannot be empty.");
                }

                request.HospitalId = hospitalId;

                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdateHospital ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateHospital for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating hospital", Error = ex.Message });
            }
        }

        [HttpGet("{hospitalId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalDetailsResponseModel>> GetHospitalById(Guid hospitalId)
        {
            _logger.LogInformation("GetHospitalById started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    return BadRequest("Hospital ID is required and cannot be empty.");
                }

                var request = new GetHospitalDetailsRequestModel(hospitalId);
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetHospitalById ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalById for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving hospital details", Error = ex.Message });
            }
        }

        [HttpGet("users/{userId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalUsersResponseModel>> GetHospitalUserById(Guid userId)
        {
            _logger.LogInformation("GetHospitalUserById started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                if(userId == Guid.Empty )
                {
                    return BadRequest("User ID is required and cannot be empty.");
                }
                var request = new GetHospitalUsersRequestModel(userId);
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetHospitalUserById ended for userId: {UserId}", userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalUserById for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving hospital user", Error = ex.Message });
            }
        }

        [HttpGet("analysis/hospitalId={hospitalId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalOverallAnalysisResponseModel>> GetHospitalOverallAnalysis(Guid hospitalId)
        {
            _logger.LogInformation("GetAllHospitals started at {Time}", DateTime.UtcNow);
            try
            {
                GetHospitalOverallAnalysisRequestModel requestModel = new() { HospitalId = hospitalId };
                var response = await _mediator.Send(requestModel);
                _logger.LogInformation("GetAllHospitals ended");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllHospitals");
                return StatusCode(500, new { Message = "An error occurred while retrieving all hospitals", Error = ex.Message });
            }
        }
    }
}
