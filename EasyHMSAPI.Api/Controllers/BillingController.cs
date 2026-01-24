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
    [Route("billing")]
    [ApiController]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AdminController> _logger;
        public BillingController(IMediator mediator, ILogger<AdminController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("config/changes")]
        public async Task<ActionResult<UpsertBillingChangesResponseModel>> UpsertBillingChanges([FromBody] UpsertBillingChangesRequestModel request)
        {
            _logger.LogInformation("UpsertBillingChanges started at {Time}", DateTime.UtcNow);
            UpsertBillingChangesResponseModel responseModel = new();
            try
            {
                if (request.HospitalId == Guid.Empty)
                {
                    responseModel.Success = false;
                    responseModel.Message = "HospitalId is required.";
                }
                else
                {
                    request.CurrentDateTime = DateTime.UtcNow;
                    request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                    responseModel = await _mediator.Send(request);
                    _logger.LogInformation("UpsertBillingChanges ended");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in UpsertBillingChanges . Error: {Error}", ex);
                responseModel.Success = false;
                responseModel.Message = "An error occurred while creating the charges." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(responseModel);
        }

        [HttpGet("config/charges/hospitalId={hosppitalId}")]
        public async Task<ActionResult<GetBillingChargesResponseModel>> GetBillingCharges(Guid hospitalId)
        {
            _logger.LogInformation("etBillingCharges started at {Time}", DateTime.UtcNow);
            GetBillingChargesResponseModel responseModel = new();
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    responseModel.Success = false;
                    responseModel.Message = "HospitalId is required.";
                }
                else
                {
                    GetBillingChargesRequestModel requestModel = new()
                    {
                        HospitalId = hospitalId
                    };
                    responseModel = await _mediator.Send(requestModel);
                    _logger.LogInformation("etBillingCharges ended");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in etBillingCharges . Error: {Error}", ex);
                responseModel.Success = false;
                responseModel.Message = "An error occurred while fetching the charges." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(responseModel);
        }

        [HttpDelete("config/charges")]
        public async Task<ActionResult<DeleteBillingChargesResponseModel>> DeleteBillingCharges([FromBody] DeleteBillingChargesRequestModel request)
        {
            _logger.LogInformation("etBillingCharges started at {Time}", DateTime.UtcNow);
            DeleteBillingChargesResponseModel  responseModel = new();
            try
            {
                if (request.HospitalId == Guid.Empty || request.ChargeItemId == Guid.Empty)
                {
                    responseModel.Success = false;
                    responseModel.Message = "Invalid hospitalId or chargeItemId";
                }
                else
                {
                    responseModel = await _mediator.Send(request);
                    _logger.LogInformation("etBillingCharges ended");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in etBillingCharges . Error: {Error}", ex);
                responseModel.Success = false;
                responseModel.Message = "An error occurred while fetching the charges." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(responseModel);
        }
    }
}
