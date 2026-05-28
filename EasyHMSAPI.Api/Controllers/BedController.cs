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
    [Route("bed")]
    [Authorize]
    public class BedController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<BedController> _logger;

        public BedController(IMediator mediator, ILogger<BedController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("master")]
        public async Task<ActionResult<GetBedMastersResponseModel>> GetBedMasters([FromQuery] Guid hospitalId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetBedMastersRequestModel { HospitalId = hospitalId, Page = page, PageSize = pageSize };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBedMasters for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("master/{bedId}")]
        public async Task<ActionResult<BedMasterDetailResponseModel>> GetBedById(Guid bedId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || bedId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and BedId are required." });

            try
            {
                var request = new GetBedMasterByIdRequestModel { HospitalId = hospitalId, BedId = bedId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBedById for bedId: {BedId}", bedId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("master")]
        public async Task<ActionResult<UpsertBedMasterResponseModel>> UpsertBedMaster([FromBody] UpsertBedMasterRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertBedMaster for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
