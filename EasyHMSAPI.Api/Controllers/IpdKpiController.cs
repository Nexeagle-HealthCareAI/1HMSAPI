using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    // IPD operations KPI dashboard — BOR, ALOS, bed turnaround, discharge TAT, readmission rate.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("ipd-kpi")]
    [Authorize]
    public class IpdKpiController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<IpdKpiController> _logger;

        public IpdKpiController(IMediator mediator, ILogger<IpdKpiController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<GetIpdKpiDashboardResponseModel>> GetDashboard(
            [FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (toDate < fromDate)
                return BadRequest(new { Message = "toDate must not be before fromDate." });

            try
            {
                var response = await _mediator.Send(new GetIpdKpiDashboardRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDashboard for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing the KPI dashboard." });
            }
        }
    }
}
