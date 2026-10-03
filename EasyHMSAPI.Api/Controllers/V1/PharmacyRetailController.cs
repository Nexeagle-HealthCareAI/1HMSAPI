using System;
using System.Threading.Tasks;
using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [ServiceFilter(typeof(HospitalAccessFilter))]
    [RequiresPermission("pharmacy")]
    public class PharmacyRetailController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PharmacyRetailController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("{hospitalId}/checkout")]
        public async Task<IActionResult> Checkout(Guid hospitalId, [FromBody] PharmacyRetailCheckoutCommand request)
        {
            request.HospitalId = hospitalId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User);
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
            // One key per checkout attempt (the POS sends it; the offline outbox replays it) so a double click
            // or retry returns the first sale instead of selling twice.
            request.IdempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
            var result = await _mediator.Send(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
    }
}
