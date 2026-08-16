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
    [Route("expenses")]
    [Authorize]
    [RequiresPermission("billing")]
    public class ExpensesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ExpensesController> _logger;

        public ExpensesController(IMediator mediator, ILogger<ExpensesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetExpensesResponseModel>> GetExpenses(
            [FromQuery] Guid hospitalId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
            [FromQuery] string? category, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetExpensesRequestModel
                {
                    HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate,
                    Category = category, Search = search, Page = page, PageSize = pageSize
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetExpenses for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut]
        public async Task<ActionResult<UpsertExpenseResponseModel>> UpsertExpense([FromQuery] Guid hospitalId, [FromBody] UpsertExpenseRequestModel request)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (string.IsNullOrWhiteSpace(request.CategoryCode))
                return BadRequest(new { Message = "Category is required." });

            try
            {
                request.HospitalId = hospitalId;
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (ArgumentException aex)
            {
                return BadRequest(new { aex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertExpense for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Adds several expense lines (same category/date/vendor/payment-mode/status, each with its
        // own amount + reason) in one call -- e.g. logging today's FOOD spend as separate lines.
        [HttpPost("bulk")]
        public async Task<ActionResult<BulkAddExpenseResponseModel>> BulkAddExpense([FromQuery] Guid hospitalId, [FromBody] BulkAddExpenseRequestModel request)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (string.IsNullOrWhiteSpace(request.CategoryCode))
                return BadRequest(new { Message = "Category is required." });

            try
            {
                request.HospitalId = hospitalId;
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BulkAddExpense for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpDelete]
        public async Task<ActionResult<DeleteExpenseResponseModel>> DeleteExpense([FromQuery] Guid hospitalId, [FromQuery] Guid expenseId)
        {
            if (hospitalId == Guid.Empty || expenseId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and expenseId are required." });

            try
            {
                var request = new DeleteExpenseRequestModel { HospitalId = hospitalId, ExpenseId = expenseId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteExpense for expenseId: {ExpenseId}", expenseId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
