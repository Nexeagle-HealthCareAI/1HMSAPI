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
    // Vendor/supplier master — feeds the procurement backbone (Indent/PO/GRN) and Batch.VendorId.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory/vendors")]
    [Authorize]
    public class VendorController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<VendorController> _logger;

        public VendorController(IMediator mediator, ILogger<VendorController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetVendorsResponseModel>> GetVendors([FromQuery] Guid hospitalId, [FromQuery] bool includeInactive = false)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetVendorsRequestModel { HospitalId = hospitalId, IncludeInactive = includeInactive });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetVendors for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching vendors." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<UpsertVendorResponseModel>> UpsertVendor([FromBody] UpsertVendorRequestModel request)
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
                _logger.LogError(ex, "Error in UpsertVendor for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the vendor." });
            }
        }
    }
}
