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
    // Pharmacy Phase 3b — statutory print fields (DL numbers, FSSAI, registered pharmacist,
    // return policy) shown on pharmacy receipts/invoices. Separate from the general
    // InvoicePrintSettings font/margin config.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("pharmacy-settings")]
    [Authorize]
    [RequiresPermission("pharmacy")]
    public class PharmacySettingsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PharmacySettingsController> _logger;

        public PharmacySettingsController(IMediator mediator, ILogger<PharmacySettingsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("print")]
        public async Task<ActionResult<GetPharmacyPrintSettingsResponseModel>> GetPrintSettings([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPharmacyPrintSettingsRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPrintSettings for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching pharmacy print settings." });
            }
        }

        [HttpPut("print")]
        public async Task<ActionResult<UpsertPharmacyPrintSettingsResponseModel>> UpsertPrintSettings([FromBody] UpsertPharmacyPrintSettingsRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

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
                _logger.LogError(ex, "Error in UpsertPrintSettings for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving pharmacy print settings." });
            }
        }
    }
}
