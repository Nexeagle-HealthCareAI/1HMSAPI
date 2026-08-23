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
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [ServiceFilter(typeof(HospitalAccessFilter))]
    [RequiresPermission("pathology")]
    public class PathologyCatalogController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PathologyCatalogController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{hospitalId}")]
        public async Task<IActionResult> GetTests(Guid hospitalId, [FromQuery] string? searchTerm, [FromQuery] string? category)
        {
            var query = new GetPathologyTestsQuery
            {
                HospitalId = hospitalId,
                SearchTerm = searchTerm,
                Category = category
            };
            
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("{hospitalId}")]
        public async Task<IActionResult> CreateTest(Guid hospitalId, [FromBody] CreatePathologyTestRequestModel request)
        {
            request.HospitalId = hospitalId;
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPut("{hospitalId}/{testId}")]
        public async Task<IActionResult> UpdateTest(Guid hospitalId, Guid testId, [FromBody] UpdatePathologyTestRequestModel request)
        {
            request.HospitalId = hospitalId;
            request.TestId = testId;
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        // --- Report Templates ---

        [HttpGet("{hospitalId}/templates")]
        public async Task<IActionResult> GetTemplates(Guid hospitalId)
        {
            var query = new GetPathologyReportTemplatesQuery
            {
                HospitalId = hospitalId
            };
            
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("{hospitalId}/templates")]
        public async Task<IActionResult> CreateTemplate(Guid hospitalId, [FromBody] CreatePathologyReportTemplateRequestModel request)
        {
            request.HospitalId = hospitalId;
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPut("{hospitalId}/templates/{templateId}")]
        public async Task<IActionResult> UpdateTemplate(Guid hospitalId, Guid templateId, [FromBody] UpdatePathologyReportTemplateRequestModel request)
        {
            request.HospitalId = hospitalId;
            request.TemplateId = templateId;
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPost("{hospitalId}/templates/upload")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadTemplate(Guid hospitalId, [FromForm] UploadPathologyReportTemplateRequestModel request)
        {
            request.HospitalId = hospitalId;
            var result = await _mediator.Send(request);
            return Ok(result);
        }
    }
}
