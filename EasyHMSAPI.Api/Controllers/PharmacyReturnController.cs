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
    // Pharmacy Phase 3d — patient returns/restock, return-to-vendor (RTV), and analytics.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("pharmacy-returns")]
    [Authorize]
    [RequiresPermission("pharmacy")]
    public class PharmacyReturnController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PharmacyReturnController> _logger;

        public PharmacyReturnController(IMediator mediator, ILogger<PharmacyReturnController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("invoice-lines")]
        public async Task<ActionResult<GetReturnableInvoiceLinesResponseModel>> GetReturnableInvoiceLines([FromQuery] Guid hospitalId, [FromQuery] string invoiceNo)
        {
            if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(invoiceNo))
                return BadRequest(new { Message = "hospitalId and invoiceNo are required." });

            try
            {
                var response = await _mediator.Send(new GetReturnableInvoiceLinesRequestModel { HospitalId = hospitalId, InvoiceNo = invoiceNo });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReturnableInvoiceLines for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while scanning the invoice for returnable lines." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<CreatePharmacyReturnResponseModel>> CreatePharmacyReturn([FromBody] CreatePharmacyReturnRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreatePharmacyReturn for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the return." });
            }
        }

        [HttpGet("rtv/eligible-batches")]
        public async Task<ActionResult<GetRtvEligibleBatchesResponseModel>> GetRtvEligibleBatches([FromQuery] Guid hospitalId, [FromQuery] Guid vendorId, [FromQuery] int daysWindow = 60)
        {
            if (hospitalId == Guid.Empty || vendorId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and vendorId are required." });

            try
            {
                var response = await _mediator.Send(new GetRtvEligibleBatchesRequestModel { HospitalId = hospitalId, VendorId = vendorId, DaysWindow = daysWindow });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRtvEligibleBatches for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while compiling RTV-eligible batches." });
            }
        }

        [HttpGet("rtv")]
        public async Task<ActionResult<GetVendorReturnsResponseModel>> GetVendorReturns([FromQuery] Guid hospitalId, [FromQuery] Guid? vendorId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetVendorReturnsRequestModel { HospitalId = hospitalId, VendorId = vendorId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetVendorReturns for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching vendor returns." });
            }
        }

        [HttpPost("rtv")]
        public async Task<ActionResult<CreateVendorReturnResponseModel>> CreateVendorReturn([FromBody] CreateVendorReturnRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateVendorReturn for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while generating the vendor return note." });
            }
        }

        [HttpGet("analytics/sales-trend")]
        public async Task<ActionResult<GetPharmacySalesTrendResponseModel>> GetSalesTrend([FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string groupBy = "DAY")
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPharmacySalesTrendRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate, GroupBy = groupBy });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSalesTrend for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing the sales trend." });
            }
        }

        [HttpGet("analytics/abc")]
        public async Task<ActionResult<GetPharmacyAbcAnalysisResponseModel>> GetAbcAnalysis([FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPharmacyAbcAnalysisRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAbcAnalysis for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing the ABC analysis." });
            }
        }

        [HttpGet("analytics/gst-liability")]
        public async Task<ActionResult<GetPharmacyGstLiabilityResponseModel>> GetGstLiability([FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPharmacyGstLiabilityRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetGstLiability for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing GST liability." });
            }
        }

        [HttpGet("analytics/expiry-loss-prevented")]
        public async Task<ActionResult<GetPharmacyExpiryLossPreventedResponseModel>> GetExpiryLossPrevented([FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPharmacyExpiryLossPreventedRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetExpiryLossPrevented for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing expiry loss prevented." });
            }
        }
    }
}
