using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    // Pharmacy billing register — a raw per-invoice list of pharmacy sales (counter + IPD-posted),
    // separate from PharmacyReturnController's aggregated analytics endpoints.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("pharmacy-billing")]
    [Authorize]
    [RequiresPermission("pharmacy")]
    public class PharmacyBillingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PharmacyBillingController> _logger;

        public PharmacyBillingController(IMediator mediator, ILogger<PharmacyBillingController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("history")]
        public async Task<ActionResult<GetPharmacyBillingHistoryResponseModel>> GetHistory(
            [FromQuery] Guid hospitalId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPharmacyBillingHistoryRequestModel
                {
                    HospitalId = hospitalId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHistory for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the pharmacy billing history." });
            }
        }
    }
}
