using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
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
    /// <summary>
    /// Hospital-chain management: an owner (Admin/AdminDoctor) creates a chain and onboards more
    /// hospitals into it. The owner is always resolved from the signed-in token, never the body.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("chains")]
    [Authorize]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    [RequiresPermission("admin_panel")]
    public class ChainsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ChainsController> _logger;

        public ChainsController(IMediator mediator, ILogger<ChainsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<CreateHospitalChainResponseModel>> CreateChain([FromBody] CreateHospitalChainRequestModel request)
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.OwnerUserId = userId.Value;
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chain for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while creating the chain." });
            }
        }

        [HttpGet("mine")]
        public async Task<ActionResult<GetMyChainResponseModel>> GetMyChain()
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                var response = await _mediator.Send(new GetMyChainRequestModel { UserId = userId.Value });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chain for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while loading the chain." });
            }
        }

        // Doctors across the caller's chain + which hospitals each works at.
        [HttpGet("mine/doctors")]
        public async Task<ActionResult<GetChainDoctorsResponseModel>> GetChainDoctors()
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                var response = await _mediator.Send(new GetChainDoctorsRequestModel { UserId = userId.Value });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chain doctors for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while loading chain doctors." });
            }
        }

        // Add an existing doctor to a hospital in the caller's chain.
        [HttpPost("{chainId}/doctors")]
        public async Task<ActionResult<AddDoctorToHospitalResponseModel>> AddDoctor(Guid chainId, [FromBody] AddDoctorToHospitalRequestModel request)
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.CallerUserId = userId.Value;
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding doctor to hospital in chain {ChainId}", chainId);
                return StatusCode(500, new { Message = "An error occurred while adding the doctor." });
            }
        }

        // Onboard a new hospital into the caller's chain (reuses hospital registration with ChainId).
        [HttpPost("{chainId}/hospitals")]
        public async Task<ActionResult<HospitalRegisterResponseModel>> OnboardHospital(Guid chainId, [FromBody] HospitalRegisterRequestModel request)
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.UserId = userId.Value;          // owner resolved from token, not the body
                request.ChainId = chainId;
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error onboarding hospital into chain {ChainId}", chainId);
                return StatusCode(500, new { Message = "An error occurred while onboarding the hospital." });
            }
        }
    }
}
