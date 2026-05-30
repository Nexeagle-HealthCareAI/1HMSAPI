using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("payment")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IMediator mediator, ILogger<PaymentController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("add-event")]
        public async Task<ActionResult<AddPaymentEventResponseModel>> AddPaymentEvent([FromBody] AddPaymentEventRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddPaymentEvent for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred while recording the payment." });
            }
        }
    }
}
