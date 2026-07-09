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
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("package-type")]
    [Authorize]
    public class PackageTypeController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PackageTypeController> _logger;

        public PackageTypeController(IMediator mediator, ILogger<PackageTypeController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<ActionResult<GetPackageTypesResponseModel>> GetPackageTypes([FromQuery] Guid hospitalId, [FromQuery] bool includeInactive = false)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetPackageTypesRequestModel { HospitalId = hospitalId, IncludeInactive = includeInactive };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPackageTypes for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("upsert")]
        public async Task<ActionResult<UpsertPackageTypeResponseModel>> UpsertPackageType([FromBody] UpsertPackageTypeRequestModel request)
        {
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
                _logger.LogError(ex, "Error in UpsertPackageType for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
