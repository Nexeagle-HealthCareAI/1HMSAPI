using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("departments")]
    public class DepartmentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DepartmentsController> _logger;
        public DepartmentsController(IMediator mediator, ILogger<DepartmentsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{hospitalId}")]
        [Authorize]
        public async Task<IActionResult> GetDepartments([FromRoute] Guid hospitalId)
        {
            _logger.LogInformation("GetDepartments started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                var request = new GetDepartmentsRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetDepartments ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDepartments for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving departments", Error = ex.Message });
            }
        }

        [HttpGet("global")]
        [Authorize]
        public async Task<IActionResult> GetGlobalDepartments()
        {
            _logger.LogInformation("GetGlobalDepartments started at {Time}", DateTime.UtcNow);
            try
            {
                var request = new GetGlobalDepartmentsRequestModel();
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetGlobalDepartments ended");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetGlobalDepartments");
                return StatusCode(500, new { Message = "An error occurred while retrieving global departments", Error = ex.Message });
            }
        }

        [HttpGet("hospital/{hospitalId}")]
        [Authorize]
        public async Task<IActionResult> GetHospitalDepartments([FromRoute] Guid hospitalId)
        {
            _logger.LogInformation("GetHospitalDepartments started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                var request = new GetHospitalDepartmentsRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetHospitalDepartments ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalDepartments for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving hospital departments", Error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequestModel request)
        {
            _logger.LogInformation("CreateDepartment started at {Time} with request: {@Request}", DateTime.UtcNow, request);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("CreateDepartment ended with response: {@Response}", response);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateDepartment");
                return StatusCode(500, new { Message = "An error occurred while creating department", Error = ex.Message });
            }
        }

        [HttpPut("{departmentId}")]
        [Authorize]
        public async Task<IActionResult> UpdateDepartment([FromRoute] Guid departmentId, [FromBody] UpdateDepartmentRequestModel request)
        {
            request.DepartmentId = departmentId;
            _logger.LogInformation("UpdateDepartment started at {Time} for departmentId: {DepartmentId} with request: {@Request}", DateTime.UtcNow, departmentId, request);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdateDepartment ended for departmentId: {DepartmentId} with response: {@Response}", departmentId, response);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateDepartment for departmentId: {DepartmentId}", departmentId);
                return StatusCode(500, new { Message = "An error occurred while updating department", Error = ex.Message });
            }
        }

        [HttpPatch("{departmentId}/toggle-status")]
        [Authorize]
        public async Task<IActionResult> ToggleDepartmentStatus([FromRoute] Guid departmentId)
        {
            _logger.LogInformation("ToggleDepartmentStatus started at {Time} for departmentId: {DepartmentId}", DateTime.UtcNow, departmentId);
            try
            {
                var request = new ToggleDepartmentStatusRequestModel { DepartmentId = departmentId };
                var response = await _mediator.Send(request);
                _logger.LogInformation("ToggleDepartmentStatus ended for departmentId: {DepartmentId} with response: {@Response}", departmentId, response);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ToggleDepartmentStatus for departmentId: {DepartmentId}", departmentId);
                return StatusCode(500, new { Message = "An error occurred while toggling department status", Error = ex.Message });
            }
        }
    }
}
