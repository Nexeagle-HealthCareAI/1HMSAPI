using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    // Staff-facing "Lead Generation" page (easyHMSWeb) -- hospital-scoped marketing leads
    // captured from Doctor Dekho (NexEagleWebsite) and the WhatsApp bot. See RecordLeadHandler
    // (public/leads) for the write side.
    [ExcludeFromCodeCoverage]
    [Route("leads")]
    [ApiController]
    public class LeadsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<LeadsController> _logger;

        public LeadsController(IMediator mediator, ILogger<LeadsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Route/param shape mirrors HospitalsController.GetHospitalOverallAnalysis exactly
        // ("resource/analysis/hospitalId={id}" convention) -- HospitalAccessFilter picks up the
        // hospitalId route param automatically, no extra wiring needed.
        [HttpGet("hospitalId={hospitalId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalLeadsResponseModel>> GetHospitalLeads(
            Guid hospitalId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? source = null,
            [FromQuery] string? leadType = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var response = await _mediator.Send(new GetHospitalLeadsRequestModel
                {
                    HospitalId = hospitalId,
                    Page = page,
                    PageSize = pageSize,
                    Source = source,
                    LeadType = leadType,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                });
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LeadsController.GetHospitalLeads for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching leads." });
            }
        }
    }
}
