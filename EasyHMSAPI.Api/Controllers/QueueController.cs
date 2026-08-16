using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    // Staff-facing OPD queue actions for the QR check-in flow -- reception marking a walk-in
    // arrived without a geofence check, and a doctor calling/skipping patients. Bare route + JWT
    // convention (matches DoctorsController/CalendarServicesController), not PublicController's
    // anonymous shape: these are actions taken by hospital staff, not a patient's own phone.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("queue")]
    [Authorize]
    [EasyHMSAPI.Api.Common.RequiresPermission("appointment_scheduler")]
    public class QueueController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<QueueController> _logger;

        public QueueController(IMediator mediator, ILogger<QueueController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("{doctorId:guid}/mark-arrived")]
        public async Task<ActionResult<IssueQueueTokenResponseModel>> MarkArrived(Guid doctorId, [FromBody] MarkArrivedRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                request.DoctorId = doctorId;
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QueueController.MarkArrived for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while marking the patient arrived." });
            }
        }

        [HttpPost("{doctorId:guid}/call")]
        public async Task<ActionResult<CallQueueResponseModel>> Call(Guid doctorId, [FromBody] CallNextPatientRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                request.DoctorId = doctorId;
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QueueController.Call for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while calling the next patient." });
            }
        }

        [HttpPost("{doctorId:guid}/skip")]
        public async Task<ActionResult<CallQueueResponseModel>> Skip(Guid doctorId, [FromBody] SkipCurrentPatientRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                request.DoctorId = doctorId;
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QueueController.Skip for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while skipping the patient." });
            }
        }
    }
}
