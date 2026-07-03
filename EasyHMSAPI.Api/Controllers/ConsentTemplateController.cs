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
    [Route("consent-template")]
    [Authorize]
    public class ConsentTemplateController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ConsentTemplateController> _logger;

        public ConsentTemplateController(IMediator mediator, ILogger<ConsentTemplateController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetConsentTemplatesResponseModel>> GetTemplates(
            [FromQuery] Guid hospitalId, [FromQuery] string? typeCode, [FromQuery] string? language, [FromQuery] bool activeOnly = true)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetConsentTemplatesRequestModel { HospitalId = hospitalId, TypeCode = typeCode, Language = language, ActiveOnly = activeOnly });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTemplates for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while loading consent templates." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<UpsertConsentTemplateResponseModel>> Upsert([FromBody] UpsertConsentTemplateRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.TypeCode))
                return BadRequest(new { Message = "hospitalId and typeCode are required." });

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
                _logger.LogError(ex, "Error in Upsert for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the consent template." });
            }
        }
    }
}
