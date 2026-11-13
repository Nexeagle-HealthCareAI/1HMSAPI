using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers.V1
{
    /// <summary>
    /// Staff-facing management of biometric attendance devices and of which employee each device PIN
    /// belongs to. (The device-facing push endpoint is BiometricIngestController.) Endpoints that take a
    /// record id rather than a hospitalId are authorized inside their handlers against the record's own
    /// hospital, because HospitalAccessFilter only guards requests that carry a hospitalId.
    /// </summary>
    [Route("api/v1/hr/biometric")]
    [ApiController]
    [Authorize]
    [ServiceFilter(typeof(HospitalAccessFilter))]
    public class HrBiometricController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HrBiometricController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("devices")]
        [RequiresPermission("hr.manage_employees", "hr.view_dashboard")]
        public async Task<ActionResult<GetHrBiometricDevicesResponseModel>> GetDevices([FromQuery] Guid hospitalId)
        {
            return Ok(await _mediator.Send(new GetHrBiometricDevicesRequestModel { HospitalId = hospitalId }));
        }

        [HttpPost("devices")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<RegisterHrBiometricDeviceResponseModel>> RegisterDevice([FromBody] RegisterHrBiometricDeviceRequestModel request)
        {
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
            var result = await _mediator.Send(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("devices/{deviceId}/active")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<HrBiometricActionResponseModel>> SetDeviceActive(Guid deviceId, [FromBody] SetHrBiometricDeviceActiveRequestModel request)
        {
            request.HrBiometricDeviceId = deviceId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            var result = await _mediator.Send(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("devices/{deviceId}/rotate-token")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<RotateHrBiometricDeviceTokenResponseModel>> RotateDeviceToken(Guid deviceId)
        {
            var result = await _mediator.Send(new RotateHrBiometricDeviceTokenRequestModel
            {
                HrBiometricDeviceId = deviceId,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            });
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("unmapped")]
        [RequiresPermission("hr.manage_employees", "hr.view_dashboard")]
        public async Task<ActionResult<GetUnmappedDeviceUsersResponseModel>> GetUnmappedDeviceUsers([FromQuery] Guid hospitalId)
        {
            return Ok(await _mediator.Send(new GetUnmappedDeviceUsersRequestModel { HospitalId = hospitalId }));
        }

        [HttpGet("device-users")]
        [RequiresPermission("hr.manage_employees", "hr.view_dashboard")]
        public async Task<ActionResult<GetEmployeeDeviceUsersResponseModel>> GetEmployeeDeviceUsers([FromQuery] Guid hospitalId)
        {
            return Ok(await _mediator.Send(new GetEmployeeDeviceUsersRequestModel { HospitalId = hospitalId }));
        }

        [HttpPut("employees/{employeeId}/device-user")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<MapEmployeeDeviceUserResponseModel>> MapEmployeeDeviceUser(Guid employeeId, [FromBody] MapEmployeeDeviceUserRequestModel request)
        {
            request.HrEmployeeId = employeeId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
            var result = await _mediator.Send(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("employees/{employeeId}/device-user")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<HrBiometricActionResponseModel>> UnmapEmployeeDeviceUser(Guid employeeId)
        {
            var result = await _mediator.Send(new UnmapEmployeeDeviceUserRequestModel
            {
                HrEmployeeId = employeeId,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            });
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
