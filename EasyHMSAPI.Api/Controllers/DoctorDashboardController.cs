using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [Route("doctor-dashboard")]
    [ApiController]
    public class DoctorDashboardController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DoctorDashboardController> _logger;

        public DoctorDashboardController(IMediator mediator, ILogger<DoctorDashboardController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("appointment-details")]
        public async Task<IActionResult> GetAppointmentDetails([FromQuery] string? status, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] Guid hospitalId, [FromQuery] Guid doctorId)
        {
            _logger.LogInformation("GetAppointmentDetails started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}", DateTime.UtcNow, hospitalId, doctorId);
            try
            {
                if (hospitalId == Guid.Empty)
                    return BadRequest(new { Message = "HospitalId is required." });
                if (doctorId == Guid.Empty)
                    return BadRequest(new { Message = "DoctorId is required." });

                var request = new DoctorDashboardAppointmentDetailsRequestModel
                {
                    Status = status,
                    StartDate = startDate,
                    EndDate = endDate,
                    HospitalId = hospitalId,
                    DoctorId = doctorId
                };
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetAppointmentDetails ended for hospitalId: {HospitalId}, doctorId: {DoctorId}", hospitalId, doctorId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in GetAppointmentDetails for hospitalId: {HospitalId}, doctorId: {DoctorId}. Error: {Error}", hospitalId, doctorId, ex);
                return StatusCode(500, new { Message = "An error occurred while retrieving appointment details", Error = ex.Message });
            }
        }
    }
}
