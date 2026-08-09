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
            catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                return BadRequest(new { ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertBedMaster for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // True hard delete — only succeeds if the bed has no BedAssignment history and isn't
        // currently occupied. Beds with history must be deactivated (PUT master, isActive=false)
        // instead; the database's FK on BedAssignment.BedId would reject a hard delete anyway.
        [HttpDelete("master/{bedId}")]
        public async Task<ActionResult<HardDeleteBedMasterResponseModel>> HardDeleteBedMaster(Guid bedId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || bedId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and bedId are required." });

            try
            {
                var response = await _mediator.Send(new HardDeleteBedMasterRequestModel { HospitalId = hospitalId, BedId = bedId });
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HardDeleteBedMaster for bedId: {BedId}", bedId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("master/bulk-hard-delete")]
        public async Task<ActionResult<BulkHardDeleteBedMasterResponseModel>> BulkHardDeleteBedMaster([FromBody] BulkHardDeleteBedMasterRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (request.BedIds == null || request.BedIds.Count == 0)
                return BadRequest(new { Message = "At least one bedId is required." });

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BulkHardDeleteBedMaster for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("master/bulk")]
        public async Task<ActionResult<BulkCreateBedMasterResponseModel>> BulkCreateBedMaster([FromBody] BulkCreateBedMasterRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (request.Count <= 0)
                return BadRequest(new { Message = "Number of beds must be greater than 0." });

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
                _logger.LogError(ex, "Error in BulkCreateBedMaster for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Soft-deletes (IsActive=false) every requested bed that isn't OCCUPIED; occupied beds are
        // rejected individually (see BulkDeleteBedMasterHandler) rather than failing the whole batch.
        [HttpPost("master/bulk-delete")]
        public async Task<ActionResult<BulkDeleteBedMasterResponseModel>> BulkDeleteBedMaster([FromBody] BulkDeleteBedMasterRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (request.BedIds == null || request.BedIds.Count == 0)
                return BadRequest(new { Message = "At least one bedId is required." });

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
                _logger.LogError(ex, "Error in BulkDeleteBedMaster for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("room/master")]
        public async Task<ActionResult<GetRoomsResponseModel>> GetRooms([FromQuery] Guid hospitalId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetRoomsRequestModel { HospitalId = hospitalId, Page = page, PageSize = pageSize };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRooms for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("room/master/{roomId}")]
        public async Task<ActionResult<RoomDetailResponseModel>> GetRoomById(Guid roomId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || roomId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and RoomId are required." });

            try
            {
                var request = new GetRoomByIdRequestModel { HospitalId = hospitalId, RoomId = roomId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRoomById for roomId: {RoomId}", roomId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("room/master")]
        public async Task<ActionResult<UpsertRoomResponseModel>> UpsertRoom([FromBody] UpsertRoomRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

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
                _logger.LogError(ex, "Error in UpsertRoom for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Live bed board: every bed for the hospital, with its current occupant if any.
        [HttpGet("board")]
        public async Task<ActionResult<GetBedBoardResponseModel>> GetBedBoard([FromQuery] Guid hospitalId, [FromQuery] string? wardCode = null)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetBedBoardRequestModel { HospitalId = hospitalId, WardCode = wardCode };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBedBoard for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Distinct wards for the hospital, derived from BedMaster -- there is no separate Ward
        // table. Used by the Nursing Station roster ward-picker and its ward chips.
        [HttpGet("wards")]
        public async Task<ActionResult<GetWardListResponseModel>> GetWards([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetWardListRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetWards for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Assign a bed to an admission that doesn't currently have one.
        [HttpPost("assign")]
        public async Task<ActionResult<AssignBedResponseModel>> AssignBed([FromBody] AssignBedRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.BedId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, admissionId and bedId are required." });

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
                _logger.LogError(ex, "Error in AssignBed for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Release the admission's current bed (e.g. on discharge).
        [HttpPost("release")]
        public async Task<ActionResult<ReleaseBedResponseModel>> ReleaseBed([FromBody] ReleaseBedRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in ReleaseBed for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Move an admission from its current bed to a different one.
        [HttpPost("transfer")]
        public async Task<ActionResult<TransferBedResponseModel>> TransferBed([FromBody] TransferBedRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.NewBedId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, admissionId and newBedId are required." });

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
                _logger.LogError(ex, "Error in TransferBed for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
