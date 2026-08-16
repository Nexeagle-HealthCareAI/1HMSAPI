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
    public class PathologyConfigController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PathologyConfigController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{hospitalId}")]
        public async Task<IActionResult> GetConfig(Guid hospitalId)
        {
            var query = new GetLabConfigurationQuery { HospitalId = hospitalId };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPut("{hospitalId}")]
        public async Task<IActionResult> UpdateConfig(Guid hospitalId, [FromBody] UpdateLabConfigurationCommand request)
        {
            request.HospitalId = hospitalId;
            var result = await _mediator.Send(request);
            return Ok(result);
        }
    }
}
