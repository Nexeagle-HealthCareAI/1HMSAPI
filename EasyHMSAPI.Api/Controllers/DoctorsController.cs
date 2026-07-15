using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromDescription]
    [Route("doctors")]
    [ApiController]
    public class DoctorsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DoctorsController> _logger;

        public DoctorsController(IMediator mediator, ILogger<DoctorsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<DoctorCreateResponseModel>> CreateDoctor([FromBody] DoctorCreateRequestModel request)
        {
            _logger.LogInformation("CreateDoctor started at {Time} for userId: {UserId}", DateTime.UtcNow, request.UserId);
            try
            {
                if (request.UserId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                if (string.IsNullOrEmpty(request.LicenseNumber))
                {
                    return BadRequest(new { Message = "License Number is required." });
                }

                var response = await _mediator.Send(request);
                _logger.LogInformation("CreateDoctor ended for userId: {UserId}", request.UserId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateDoctor for userId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = "An error occurred while creating the doctor profile", Error = ex.Message });
            }
        }

        [HttpGet("{userId}")]
        [Authorize]
        public async Task<ActionResult<DoctorGetResponseModel>> GetDoctor(Guid userId)
        {
            _logger.LogInformation("GetDoctor started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                if (userId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                var request = new DoctorGetRequestModel { UserId = userId };
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetDoctor ended for userId: {UserId}", userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDoctor for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving doctor details", Error = ex.Message });
            }
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<ActionResult<DoctorUpdateResponseModel>> UpdateDoctorProfile([FromBody] DoctorUpdateRequestModel request)
        {
            _logger.LogInformation("UpdateDoctorProfile started at {Time} for userId: {UserId}", DateTime.UtcNow, request.UserId);
            try
            {
                if (request.UserId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                if (request.HospitalDepartmentMappingId == Guid.Empty)
                {
                    return BadRequest(new { Message = "Hospital Department Mapping ID is required and cannot be empty." });
                }

                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdateDoctorProfile ended for userId: {UserId}", request.UserId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateDoctorProfile for userId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = "An error occurred while updating doctor profile", Error = ex.Message });
            }
        }

        [HttpGet("specializations")]
        [Authorize]
        public async Task<ActionResult<DoctorSpecializationsResponseModel>> GetSpecializations([FromQuery] Guid departmentId, [FromQuery] Guid? hospitalId, [FromQuery] bool includeGlobal = true)
        {
            _logger.LogInformation("GetSpecializations started at {Time} for departmentId: {DepartmentId}", DateTime.UtcNow, departmentId);
            try
            {
                if (departmentId == Guid.Empty)
                {
                    return BadRequest(new { Message = "departmentId is required" });
                }

                var request = new DoctorSpecializationsRequestModel
                {
                    DepartmentId = departmentId,
                    HospitalId = hospitalId,
                    IncludeGlobal = includeGlobal
                };

                var response = await _mediator.Send(request);
                _logger.LogInformation("GetSpecializations ended for departmentId: {DepartmentId}", departmentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSpecializations for departmentId: {DepartmentId}", departmentId);
                return StatusCode(500, new { Message = "An error occurred while retrieving specializations", Error = ex.Message });
            }
        }

        // Flat, hospital-wide doctor list (no department filter) — for simple pickers such as the
        // admitting-consultant selector on the IPD admit form.
        [HttpGet("hospital")]
        [Authorize]
        public async Task<ActionResult<GetHospitalDoctorsResponseModel>> GetHospitalDoctors([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetHospitalDoctorsRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalDoctors for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving hospital doctors." });
            }
        }

        // Rich, hospital-scoped doctor list for the admin Public Directory tile editor (photo,
        // license, qualification, bio, specializations, languages, contact fields) — deliberately
        // separate from GetHospitalDoctors above so that hot, simple picker never carries the extra
        // blob-storage/specialization lookups this needs.
        [HttpGet("public-directory")]
        [Authorize]
        public async Task<ActionResult<GetPublicDirectoryDoctorsResponseModel>> GetPublicDirectoryDoctors([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPublicDirectoryDoctorsRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPublicDirectoryDoctors for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving public directory doctors." });
            }
        }

        // Admin moderation list for one doctor's reviews (hidden + visible) — Public Directory's
        // review panel.
        [HttpGet("{doctorId:guid}/reviews")]
        [Authorize]
        public async Task<ActionResult<GetHospitalDoctorReviewsResponseModel>> GetDoctorReviews(Guid doctorId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetHospitalDoctorReviewsRequestModel { HospitalId = hospitalId, DoctorId = doctorId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDoctorReviews for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while retrieving reviews." });
            }
        }

        // Hide/unhide one review. hospitalId as a query param so HospitalAccessFilter gates this
        // to callers who are members of that hospital; the handler additionally confirms the
        // review itself belongs to that hospital.
        [HttpPatch("{doctorId:guid}/reviews/{reviewId:guid}/moderate")]
        [Authorize]
        public async Task<ActionResult<ModerateDoctorReviewResponseModel>> ModerateDoctorReview(Guid doctorId, Guid reviewId, [FromQuery] Guid hospitalId, [FromBody] ModerateReviewRequestBody body)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new ModerateDoctorReviewRequestModel { HospitalId = hospitalId, ReviewId = reviewId, IsHidden = body.IsHidden });
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ModerateDoctorReview for reviewId: {ReviewId}", reviewId);
                return StatusCode(500, new { Message = "An error occurred while moderating the review." });
            }
        }

        // Toggles whether one doctor shows on the platform-wide public directory. hospitalId as a
        // query param (not the body) so the global HospitalAccessFilter gates this to callers who
        // are members of that hospital — same pattern as DoctorFeesController.
        [HttpPatch("public-listing")]
        [Authorize]
        public async Task<ActionResult<UpdateDoctorPublicListingResponseModel>> UpdateDoctorPublicListing([FromQuery] Guid hospitalId, [FromBody] UpdateDoctorPublicListingRequestModel request)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (request.DoctorId == Guid.Empty)
                return BadRequest(new { Message = "doctorId is required." });

            try
            {
                request.HospitalId = hospitalId;
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateDoctorPublicListing for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating the doctor's public-listing preference." });
            }
        }
    }
}
