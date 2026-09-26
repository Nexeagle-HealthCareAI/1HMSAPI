using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Cryptography;
using System.Text;
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
        private readonly IConfiguration _configuration;

        public HrController(IMediator mediator, IConfiguration configuration)
        {
            _mediator = mediator;
            _configuration = configuration;
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

        // ─── Attendance & Biometrics ──────────────────────────────────────────

        [HttpPost("biometric-punch")]
        [AllowAnonymous] // Devices can't sign in; they authenticate with the shared ingest key below.
        public async Task<ActionResult<ProcessBiometricPunchResponseModel>> BiometricPunch(
            [FromBody] ProcessBiometricPunchRequestModel request,
            [FromHeader(Name = "X-API-KEY")] string? apiKey)
        {
            // The key comes from configuration (Hr:BiometricIngestKey), never from source: the previous
            // hardcoded "ZKTeco-Hook-Secret" was readable by anyone with repo access and identical everywhere.
            // Unconfigured = endpoint off, so it fails closed until a key is deliberately set. This is an
            // interim shared key; per-device credentials replace it once the device registry exists.
            var expectedKey = _configuration["Hr:BiometricIngestKey"];
            if (string.IsNullOrWhiteSpace(expectedKey) || expectedKey.StartsWith('<'))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Biometric ingestion is not configured." });
            }

            if (string.IsNullOrEmpty(apiKey) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(apiKey), Encoding.UTF8.GetBytes(expectedKey)))
            {
                return Unauthorized(new { message = "Invalid API Key" });
            }

            if (request.HospitalId == Guid.Empty)
            {
                return BadRequest(new { message = "hospitalId is required." });
            }

            var result = await _mediator.Send(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

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
