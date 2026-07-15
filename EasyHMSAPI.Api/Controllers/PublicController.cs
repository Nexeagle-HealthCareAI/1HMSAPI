using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Public surface for external integrations (the Nexeagle booking website and, generically,
    /// any site wanting to list/book/review publicly-listed doctors). No staff JWT — the
    /// X-Api-Key header is optional (see PublicApiKeyFilter): anonymous callers are let through,
    /// a header is only needed if a consumer wants its traffic identified/revocable. Not scoped
    /// to one hospital: GetDoctors returns every publicly-listed hospital's doctors, and
    /// GetDoctorAvailability/BookAppointment resolve HospitalId from the doctor being acted on,
    /// never from the key or the request body.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public")]
    [AllowAnonymous]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    [ServiceFilter(typeof(PublicApiKeyFilter))]
    [EnableRateLimiting("PublicBookingPolicy")]
    public class PublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PublicController> _logger;

        public PublicController(IMediator mediator, ILogger<PublicController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("doctors")]
        public async Task<ActionResult<GetPublicDoctorsResponseModel>> GetDoctors()
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorsRequestModel());
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctors");
                return StatusCode(500, new { Message = "An error occurred while fetching doctors." });
            }
        }

        [HttpGet("doctors/{doctorId:guid}/availability")]
        public async Task<ActionResult<GetPublicDoctorAvailabilityResponseModel>> GetDoctorAvailability(Guid doctorId, [FromQuery] DateTime date)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorAvailabilityRequestModel
                {
                    DoctorId = doctorId,
                    Date = date,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorAvailability for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while fetching availability." });
            }
        }

        [HttpPost("appointments")]
        public async Task<ActionResult<PublicBookAppointmentResponseModel>> BookAppointment([FromBody] PublicBookAppointmentRequestModel request)
        {
            try
            {
                request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.BookAppointment");
                return StatusCode(500, new { Message = "An error occurred while booking the appointment." });
            }
        }

        [HttpGet("doctors/{doctorId:guid}/reviews")]
        public async Task<ActionResult<GetPublicDoctorReviewsResponseModel>> GetDoctorReviews(Guid doctorId)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorReviewsRequestModel { DoctorId = doctorId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorReviews for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while fetching reviews." });
            }
        }

        [HttpPost("doctors/{doctorId:guid}/reviews")]
        public async Task<ActionResult<SubmitDoctorReviewResponseModel>> SubmitDoctorReview(Guid doctorId, [FromBody] SubmitDoctorReviewRequestModel request)
        {
            try
            {
                request.DoctorId = doctorId;
                request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.SubmitDoctorReview for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while submitting the review." });
            }
        }

        [HttpPost("doctors/{doctorId:guid}/reviews/{reviewId:guid}/helpful")]
        public async Task<ActionResult<MarkReviewHelpfulResponseModel>> MarkReviewHelpful(Guid doctorId, Guid reviewId)
        {
            try
            {
                var response = await _mediator.Send(new MarkReviewHelpfulRequestModel { ReviewId = reviewId });
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.MarkReviewHelpful for reviewId: {ReviewId}", reviewId);
                return StatusCode(500, new { Message = "An error occurred while marking the review helpful." });
            }
        }
    }
}
