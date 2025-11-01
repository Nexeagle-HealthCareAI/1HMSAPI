using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("doctors")]
    [ApiController]
    public class DoctorsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DoctorsController> _logger;

        public DoctorsController(IMediator mediator, ILogger<DoctorsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<DoctorCreateResponseModel>> CreateDoctor([FromBody] DoctorCreateRequestModel request)
        {
            _logger.LogInformation("CreateDoctor started at {Time} for userId: {UserId}", DateTime.UtcNow, request.UserId);
            try
            {
                if (request.UserId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                if (string.IsNullOrEmpty(request.LicenseNumber))
                {
                    return BadRequest(new { Message = "License Number is required." });
                }

                var response = await _mediator.Send(request);

                if (response.Success)
                {
                    _logger.LogInformation("CreateDoctor ended for userId: {UserId}", request.UserId);
                    return Ok(response);
                }

                return BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateDoctor for userId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = "An error occurred while creating the doctor profile", Error = ex.Message });
            }
        }

        [HttpGet("{userId}")]
        [Authorize]
        public async Task<ActionResult<DoctorGetResponseModel>> GetDoctor(Guid userId)
        {
            _logger.LogInformation("GetDoctor started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                if (userId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                var request = new DoctorGetRequestModel { UserId = userId };
                var response = await _mediator.Send(request);

                if (response != null)
                {
                    _logger.LogInformation("GetDoctor ended for userId: {UserId}", userId);
                    return Ok(response);
                }

                return NotFound(new { Message = "Doctor not found", UserId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDoctor for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving doctor details", Error = ex.Message });
            }
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<ActionResult<DoctorUpdateResponseModel>> UpdateDoctorProfile([FromBody] DoctorUpdateRequestModel request)
        {
            _logger.LogInformation("UpdateDoctorProfile started at {Time} for userId: {UserId}", DateTime.UtcNow, request.UserId);
            try
            {
                if (request.UserId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                if (request.HospitalDepartmentMappingId == Guid.Empty)
                {
                    return BadRequest(new { Message = "Hospital Department Mapping ID is required and cannot be empty." });
                }

                var response = await _mediator.Send(request);

                if (response.Success)
                {
                    _logger.LogInformation("UpdateDoctorProfile ended for userId: {UserId}", request.UserId);
                    return Ok(response);
                }

                return BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateDoctorProfile for userId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = "An error occurred while updating doctor profile", Error = ex.Message });
            }
        }

        [HttpGet("specializations")]
        [Authorize]
        public async Task<ActionResult<DoctorSpecializationsResponseModel>> GetSpecializations([FromQuery] Guid departmentId, [FromQuery] Guid? hospitalId, [FromQuery] bool includeGlobal = true)
        {
            _logger.LogInformation("GetSpecializations started at {Time} for departmentId: {DepartmentId}", DateTime.UtcNow, departmentId);
            try
            {
                if (departmentId == Guid.Empty)
                {
                    return BadRequest(new { Message = "departmentId is required" });
                }

                var request = new DoctorSpecializationsRequestModel
                {
                    DepartmentId = departmentId,
                    HospitalId = hospitalId,
                    IncludeGlobal = includeGlobal
                };

                var response = await _mediator.Send(request);
                _logger.LogInformation("GetSpecializations ended for departmentId: {DepartmentId}", departmentId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSpecializations for departmentId: {DepartmentId}", departmentId);
                return StatusCode(500, new { Message = "An error occurred while retrieving specializations", Error = ex.Message });
            }
        }
    }
}
