using System;
using System.Threading.Tasks;
using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers.V1
{
    // Order -> result -> report pipeline for the structured Pathology Lab workspace. Placed here
    // alongside PathologyCatalogController/PathologyConfigController; the underlying MediatR
    // commands/queries already existed (CreatePathologyOrderHandler etc.) but had no HTTP surface.
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [ServiceFilter(typeof(HospitalAccessFilter))]
    [RequiresPermission("pathology")]
    public class PathologyOrderController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PathologyOrderController> _logger;

        public PathologyOrderController(IMediator mediator, ILogger<PathologyOrderController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("{hospitalId}")]
        public async Task<IActionResult> CreateOrder(Guid hospitalId, [FromBody] CreatePathologyOrderRequestModel request)
        {
            request.HospitalId = hospitalId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating pathology order for hospital {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the order." });
            }
        }

        [HttpGet("{hospitalId}")]
        public async Task<IActionResult> GetOrders(Guid hospitalId, [FromQuery] string? status)
        {
            try
            {
                var response = await _mediator.Send(new GetPathologyOrdersQuery { HospitalId = hospitalId, Status = status });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pathology orders for hospital {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching orders." });
            }
        }

        // Powers DocBoard's "Lab Report Ready" badge -- fetched once per hospital, not per
        // appointment row. Route has two literal segments after {hospitalId}, so it's unambiguous
        // against GetOrderById's single-segment {hospitalId}/{orderId} below.
        [HttpGet("{hospitalId}/reports/ready")]
        public async Task<IActionResult> GetRecentlyApprovedReports(Guid hospitalId)
        {
            try
            {
                var response = await _mediator.Send(new GetRecentlyApprovedPathologyReportsQuery { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recently approved pathology reports for hospital {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching ready reports." });
            }
        }

        [HttpGet("{hospitalId}/{orderId}")]
        public async Task<IActionResult> GetOrderById(Guid hospitalId, Guid orderId)
        {
            try
            {
                var response = await _mediator.Send(new GetPathologyOrderByIdQuery { HospitalId = hospitalId, OrderId = orderId });
                if (response == null || response.OrderId == Guid.Empty) return NotFound();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pathology order {OrderId} for hospital {HospitalId}", orderId, hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the order." });
            }
        }

        [HttpPost("{hospitalId}/{orderId}/lines/{orderLineId}/result")]
        public async Task<IActionResult> EnterResult(Guid hospitalId, Guid orderId, Guid orderLineId, [FromBody] EnterPathologyResultCommand request)
        {
            request.HospitalId = hospitalId;
            request.OrderId = orderId;
            request.OrderLineId = orderLineId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);

            try
            {
                var success = await _mediator.Send(request);
                if (!success) return BadRequest(new { success = false, message = "Failed to enter result or order line not found." });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error entering pathology result for order line {OrderLineId}", orderLineId);
                return StatusCode(500, new { Message = "An error occurred while saving the result." });
            }
        }

        // Report-level field values (Clinical History, Comments, ... -- see LabConfiguration.
        // ReportFieldLayoutJson's "reportFields" list), once per order rather than per test line.
        [HttpPost("{hospitalId}/{orderId}/report-fields")]
        public async Task<IActionResult> SaveReportFields(Guid hospitalId, Guid orderId, [FromBody] SaveOrderReportFieldsCommand request)
        {
            request.HospitalId = hospitalId;
            request.OrderId = orderId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);

            try
            {
                var success = await _mediator.Send(request);
                if (!success) return BadRequest(new { success = false, message = "Order not found." });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report field values for order {OrderId}", orderId);
                return StatusCode(500, new { Message = "An error occurred while saving the report fields." });
            }
        }

        [HttpPost("{hospitalId}/{orderId}/lines/{orderLineId}/collect-sample")]
        public async Task<IActionResult> CollectSample(Guid hospitalId, Guid orderId, Guid orderLineId, [FromBody] CollectPathologySampleCommand request)
        {
            request.HospitalId = hospitalId;
            request.OrderId = orderId;
            request.OrderLineId = orderLineId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);

            try
            {
                var success = await _mediator.Send(request);
                if (!success) return BadRequest(new { success = false, message = "Could not mark this sample collected -- it may already have moved past Pending." });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting pathology sample for order line {OrderLineId}", orderLineId);
                return StatusCode(500, new { Message = "An error occurred while recording sample collection." });
            }
        }

        [HttpPost("{hospitalId}/{orderId}/report")]
        public async Task<IActionResult> GenerateReport(Guid hospitalId, Guid orderId, [FromBody] GeneratePathologyReportCommand request)
        {
            request.HospitalId = hospitalId;
            request.OrderId = orderId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating pathology report for order {OrderId}", orderId);
                return StatusCode(500, new { Message = "An error occurred while generating the report." });
            }
        }

        [HttpPost("{hospitalId}/{orderId}/report/{reportId}/pdf")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadReportPdf(Guid hospitalId, Guid orderId, Guid reportId, [FromForm] UploadPathologyReportPdfRequestModel request)
        {
            request.HospitalId = hospitalId;
            request.ReportId = reportId;

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading final PDF for pathology report {ReportId}", reportId);
                return StatusCode(500, new { Message = "An error occurred while uploading the report PDF." });
            }
        }
    }
}
