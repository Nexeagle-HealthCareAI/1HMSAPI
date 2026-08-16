using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Controllers
{
    [ExcludeFromCodeCoverage]
    [Route("api/v1/pathology")]
    [ApiController]
    [Authorize] // EasyHMSSecurity logic handles multi-tenancy auth
    public class PathologyController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PathologyController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("order")]
        public async Task<IActionResult> CreatePathologyOrder([FromBody] CreatePathologyOrderRequestModel request)
        {
            // Inject context from claims
            request.HospitalId = User.GetHospitalId();
            request.LoggedInUserId = User.GetUserId();
            request.LoggedInUserName = User.GetUserName();
            
            var response = await _mediator.Send(request);
            if (response.Success)
            {
                return Ok(response);
            }
            return BadRequest(response);
        }
        
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] string? status)
        {
            var query = new EasyHMSAPI.Application.RequestModels.QueryRequestModels.GetPathologyOrdersQuery
            {
                HospitalId = User.GetHospitalId(),
                Status = status
            };
            var response = await _mediator.Send(query);
            return Ok(response);
        }

        [HttpGet("orders/{orderId}")]
        public async Task<IActionResult> GetOrderById(Guid orderId)
        {
            var query = new EasyHMSAPI.Application.RequestModels.QueryRequestModels.GetPathologyOrderByIdQuery
            {
                HospitalId = User.GetHospitalId(),
                OrderId = orderId
            };
            var response = await _mediator.Send(query);
            if (response == null) return NotFound();
            return Ok(response);
        }

        [HttpPost("orders/{orderId}/lines/{orderLineId}/result")]
        public async Task<IActionResult> EnterResult(Guid orderId, Guid orderLineId, [FromBody] EnterPathologyResultCommand request)
        {
            request.HospitalId = User.GetHospitalId();
            request.OrderId = orderId;
            request.OrderLineId = orderLineId;
            request.LoggedInUserId = User.GetUserId();
            request.LoggedInUserName = User.GetUserName();

            var response = await _mediator.Send(request);
            if (response)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false, message = "Failed to enter result or order line not found." });
        }

        [HttpPost("orders/{orderId}/report")]
        public async Task<IActionResult> GenerateReport(Guid orderId, [FromBody] GeneratePathologyReportCommand request)
        {
            request.HospitalId = User.GetHospitalId();
            request.OrderId = orderId;
            request.LoggedInUserId = User.GetUserId();
            request.LoggedInUserName = User.GetUserName();

            var response = await _mediator.Send(request);
            if (response.Success)
            {
                return Ok(response);
            }
            return BadRequest(response);
        }

        [HttpPost("orders/{orderId}/report/{reportId}/approve")]
        public async Task<IActionResult> ApproveReport(Guid orderId, Guid reportId, [FromBody] ApprovePathologyReportCommand request)
        {
            request.HospitalId = User.GetHospitalId();
            request.ReportId = reportId;
            request.LoggedInUserId = User.GetUserId();
            request.LoggedInUserName = User.GetUserName();

            var result = await _mediator.Send(request);
            return Ok(new { success = result });
        }
    }
}
