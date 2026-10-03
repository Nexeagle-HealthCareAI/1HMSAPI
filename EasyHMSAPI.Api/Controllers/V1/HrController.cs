using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace EasyHMSAPI.Api.Controllers.V1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    [ServiceFilter(typeof(HospitalAccessFilter))]
    public class HrController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HrController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ─── Employees ────────────────────────────────────────────────────────
        [HttpPost("employees")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<CreateHrEmployeeResponseModel>> CreateEmployee(
            [FromBody] CreateHrEmployeeRequestModel request)
        {
            // UserId here means "who performed this creation" (audit trail), not the new
            // employee's own login -- always the caller's identity, never client-supplied.
            request.UserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            var result = await _mediator.Send(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        [HttpGet("employees")]
        public async Task<ActionResult<GetHrEmployeesResponseModel>> GetEmployees(
            [FromQuery] Guid hospitalId,
            [FromQuery] string? dept = null,
            [FromQuery] string? type = null,
            [FromQuery] int page = 1,
            [FromQuery] int take = 20)
        {
            var request = new GetHrEmployeesRequestModel
            {
                HospitalId = hospitalId,
                DepartmentId = dept,
                EmploymentType = type,
                PageNumber = page,
                PageSize = take,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        // ─── Payroll ──────────────────────────────────────────────────────────
        [HttpPost("payroll/run")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<ActionResult<RunMonthlyPayrollResponseModel>> RunMonthlyPayroll(
            [FromQuery] Guid hospitalId,
            [FromQuery] int month,
            [FromQuery] int year)
        {
            var processedByUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;

            var request = new RunMonthlyPayrollRequestModel
            {
                HospitalId = hospitalId,
                Month = month,
                Year = year,
                ProcessedByUserId = processedByUserId
            };

            var result = await _mediator.Send(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        [HttpGet("payroll/run")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<ActionResult<GetHrPayrollRunsResponseModel>> GetPayrollRuns(
            [FromQuery] Guid hospitalId,
            [FromQuery] int? month = null,
            [FromQuery] int? year = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int take = 20)
        {
            var request = new GetHrPayrollRunsRequestModel
            {
                HospitalId = hospitalId,
                Month = month,
                Year = year,
                Status = status,
                PageNumber = page,
                PageSize = take
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpGet("payroll/export-bank")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<IActionResult> ExportBankFile(
            [FromQuery] Guid hrPayrollRunId,
            [FromQuery] string format = "HDFC")
        {
            var request = new ExportBankFileRequestModel
            {
                HrPayrollRunId = hrPayrollRunId,
                BankFormat = format,
                // Always the caller's own identity, never client-supplied: the handler authorizes against the
                // hospital that owns the run (this request carries no hospitalId for HospitalAccessFilter).
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);

            if (result.Success && result.FileBytes != null)
            {
                return File(result.FileBytes, result.ContentType!, result.FileName);
            }
            return BadRequest(result.Message);
        }

        [HttpGet("payroll/{hrPayrollRunId}/payslips")]
        public async Task<IActionResult> GetPayslipsByRun(Guid hrPayrollRunId)
        {
            var request = new GetPayslipsByRunRequestModel 
            { 
                HrPayrollRunId = hrPayrollRunId,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPost("payroll/{hrPayrollRunId}/dispatch")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<IActionResult> DispatchPayslips(Guid hrPayrollRunId)
        {
            var result = await _mediator.Send(new DispatchPayslipsRequestModel
            {
                HrPayrollRunId = hrPayrollRunId,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            });
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // ─── Income-tax profile (Track A TDS) ──────────────────────────────────
        [HttpPut("employees/{hrEmployeeId}/tax-profile")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<ActionResult<UpdateEmployeeTaxProfileResponseModel>> UpdateTaxProfile(
            Guid hrEmployeeId, [FromBody] UpdateEmployeeTaxProfileRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            request.HrEmployeeId = hrEmployeeId;
            request.LoggedInUserName = User.Identity?.Name;
            var result = await _mediator.Send(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ─── Consultant Fee Config (Track B payroll) ───────────────────────────
        [HttpGet("consultant-fee-config/{hrEmployeeId}")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<ActionResult<GetConsultantFeeConfigResponseModel>> GetConsultantFeeConfig(Guid hrEmployeeId)
        {
            var result = await _mediator.Send(new GetConsultantFeeConfigRequestModel { HrEmployeeId = hrEmployeeId });
            return Ok(result);
        }

        [HttpPut("consultant-fee-config/{hrEmployeeId}")]
        [RequiresPermission("hr.manage_payroll")]
        public async Task<ActionResult<UpsertConsultantFeeConfigResponseModel>> UpsertConsultantFeeConfig(
            Guid hrEmployeeId, [FromBody] UpsertConsultantFeeConfigRequestModel request)
        {
            request.HrEmployeeId = hrEmployeeId;
            request.LoggedInUserName = User.Identity?.Name;
            var result = await _mediator.Send(request);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // ─── Leaves & Roster ──────────────────────────────────────────────────

        [HttpGet("leave-requests")]
        public async Task<ActionResult<GetHrLeaveRequestsResponseModel>> GetLeaveRequests(
            [FromQuery] Guid? hospitalId,
            [FromQuery] Guid? employeeId,
            [FromQuery] string? status)
        {
            var request = new GetHrLeaveRequestsRequestModel
            {
                HospitalId = hospitalId,
                EmployeeId = employeeId,
                Status = status,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPut("leave-requests/{leaveId}/status")]
        [RequiresPermission("hr.manage_leaves")]
        public async Task<ActionResult<DecideHrLeaveResponseModel>> DecideLeave(
            Guid leaveId,
            [FromBody] DecideHrLeaveRequestModel request)
        {
            request.LeaveId = leaveId;
            request.ApprovedByUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            var result = await _mediator.Send(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }


        [HttpGet("leave-balances")]
        public async Task<ActionResult<GetHrLeaveBalanceResponseModel>> GetLeaveBalance(
            [FromQuery] Guid employeeId,
            [FromQuery] int? year)
        {
            var request = new GetHrLeaveBalanceRequestModel
            {
                EmployeeId = employeeId,
                Year = year,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpGet("shifts")]
        public async Task<ActionResult<GetHrHospitalShiftsResponseModel>> GetShifts(
            [FromQuery] Guid hospitalId)
        {
            // Shifts are public knowledge across the hospital generally
            var request = new GetHrHospitalShiftsRequestModel { HospitalId = hospitalId };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpGet("rosters")]
        public async Task<ActionResult<GetHrDutyRostersResponseModel>> GetDutyRosters(
            [FromQuery] Guid hospitalId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var request = new GetHrDutyRostersRequestModel
            {
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = endDate,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpGet("attendance-today")]
        public async Task<ActionResult<GetHrAttendanceTodayResponseModel>> GetAttendanceToday(
            [FromQuery] Guid hospitalId,
            [FromQuery] DateTime date)
        {
            var request = new GetHrAttendanceTodayRequestModel
            {
                HospitalId = hospitalId,
                Date = DateOnly.FromDateTime(date),
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        // ─── Attendance ───────────────────────────────────────────────────────
        // Biometric device scans are received by BiometricIngestController (per-device credentials) and
        // ZktecoPushController; devices are managed in HrBiometricController.

        [HttpGet("attendance/exceptions")]
        [RequiresPermission("hr.view_dashboard")]
        public async Task<ActionResult<GetAttendanceExceptionsResponseModel>> GetExceptions(
            [FromQuery] Guid hospitalId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var request = new GetAttendanceExceptionsRequestModel
            {
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = endDate
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPut("attendance/{employeeId}/override")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<SetAttendanceOverrideResponseModel>> SetAttendanceOverride(
            Guid employeeId, [FromBody] SetAttendanceOverrideRequestModel request)
        {
            request.HrEmployeeId = employeeId;
            request.LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            var result = await _mediator.Send(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ─── Payroll calendar policy (weekly offs / holidays payable) ──────────
        [HttpGet("payroll-settings")]
        [RequiresPermission("hr.manage_employees", "hr.view_dashboard")]
        public async Task<ActionResult<GetPayrollSettingsResponseModel>> GetPayrollSettings([FromQuery] Guid hospitalId, [FromQuery] int? year = null)
        {
            var result = await _mediator.Send(new GetPayrollSettingsRequestModel { HospitalId = hospitalId, Year = year });
            return Ok(result);
        }

        [HttpPut("payroll-settings")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<SimpleHrResponseModel>> SavePayrollSettings([FromQuery] Guid hospitalId, [FromBody] PayrollSettingsDto settings)
        {
            var result = await _mediator.Send(new SavePayrollSettingsRequestModel
            {
                HospitalId = hospitalId,
                UserId = UserContextHelper.GetUserId(User) ?? Guid.Empty,
                Settings = settings ?? new PayrollSettingsDto(),
            });
            return Ok(result);
        }

        [HttpPost("holidays")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<SimpleHrResponseModel>> AddHoliday([FromQuery] Guid hospitalId, [FromBody] AddHolidayRequestModel request)
        {
            request.HospitalId = hospitalId;
            request.UserId = UserContextHelper.GetUserId(User) ?? Guid.Empty;
            var result = await _mediator.Send(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("holidays/{holidayId:guid}")]
        [RequiresPermission("hr.manage_employees")]
        public async Task<ActionResult<SimpleHrResponseModel>> DeleteHoliday([FromQuery] Guid hospitalId, Guid holidayId)
        {
            var result = await _mediator.Send(new DeleteHolidayRequestModel { HospitalId = hospitalId, HrHolidayId = holidayId });
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ─── Dashboard & KPI ──────────────────────────────────────────────────
        [HttpGet("kpi-summary")]
        [RequiresPermission("hr.view_dashboard")]
        public async Task<ActionResult<GetHrKpiSummaryResponseModel>> GetKpiSummary([FromQuery] Guid hospitalId)
        {
            var request = new GetHrKpiSummaryRequestModel 
            { 
                HospitalId = hospitalId,
                LoggedInUserId = UserContextHelper.GetUserId(User) ?? Guid.Empty
            };
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpGet("license-alerts")]
        [RequiresPermission("hr.view_dashboard")]
        public async Task<ActionResult<GetLicenseExpiryAlertsResponseModel>> GetLicenseAlerts([FromQuery] Guid hospitalId)
        {
            var request = new GetLicenseExpiryAlertsRequestModel { HospitalId = hospitalId };
            var result = await _mediator.Send(request);
            return Ok(result);
        }
    }
}
