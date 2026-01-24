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
    [Route("invoice-settings")]
    [ApiController]
    [Authorize]
    public class InvoiceSettingsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AdminController> _logger;
        public InvoiceSettingsController(IMediator mediator, ILogger<AdminController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("hospitalId={hospitalId}")]
        public async Task<ActionResult<GetInvoiceSettingsResponseModel>> GetInvoiceSettings(Guid hospitalId)
        {
            _logger.LogInformation("GetInvoiceSettings started at {Time}", DateTime.UtcNow);
            GetInvoiceSettingsResponseModel responseModel = new();
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    responseModel.Success = false;
                    responseModel.Message = "Invalid hospitalId or doctorId";
                }
                else
                {
                    GetInvoiceSettingsRequestModel requestModel = new()
                    {
                        HospitalId = hospitalId
                    };
                    responseModel = await _mediator.Send(requestModel);
                    _logger.LogInformation("GetInvoiceSettings ended");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in GetInvoiceSettings . Error: {Error}", ex);
                responseModel.Success = false;
                responseModel.Message = "An error occurred while fetching the invoice settings." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(responseModel);
        }

        [HttpPost]
        public async Task<ActionResult<UpsertInvoiceSettingsResponseModel>> UpsertInvoiceSettings([FromBody] UpsertInvoiceSettingsRequestModel request)
        {
            _logger.LogInformation("GetInvoiceSettings started at {Time}", DateTime.UtcNow);
            UpsertInvoiceSettingsResponseModel responseModel = new();
            try
            {
                if (request.HospitalId == Guid.Empty)
                {
                    responseModel.Success = false;
                    responseModel.Message = "Invalid hospitalId";
                }
                else
                {
                    var userIdClaim = User.FindFirst("userId")?.Value;
                    if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
                    {
                        request.CurrentDateTime = DateTime.UtcNow;
                        request.LoggedInUserId = userId;

                        if (request.LoggedInUserId == Guid.Empty)
                        {
                            responseModel.Success = false;
                            responseModel.Message = "Invalid logged in user.";
                            return Ok(responseModel);
                        }
                        else
                        {
                            responseModel = await _mediator.Send(request);
                            _logger.LogInformation("GetInvoiceSettings ended");
                        }
                    }
                    else
                    {
                        responseModel.Success = false;
                        responseModel.Message = "Invalid logged in user.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in GetInvoiceSettings . Error: {Error}", ex);
                responseModel.Success = false;
                responseModel.Message = "An error occurred while fetching the invoice settings." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(responseModel);
        }

        [HttpPost("upload/template")]
        public async Task<ActionResult<UploadInvoiceSettingsTemplateResponseModel>> UploadInvoiceSettingsTemplate([FromQuery] UploadInvoiceSettingsTemplateRequestModel request)
        {
            _logger.LogInformation("UploadInvoiceSettingsTemplate started at {Time}", DateTime.UtcNow);
            UploadInvoiceSettingsTemplateResponseModel responseModel = new();
            try
            {
                if (request.HospitalId == Guid.Empty)
                {
                    responseModel.Success = false;
                    responseModel.Message = "Invalid hospitalId";
                }
                else
                {
                    var userIdClaim = User.FindFirst("userId")?.Value;
                    if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
                    {
                        request.LoggedInUserId = userId;

                        if (request.LoggedInUserId == Guid.Empty)
                        {
                            responseModel.Success = false;
                            responseModel.Message = "Invalid logged in user.";
                            return Ok(responseModel);
                        }
                        else
                        {
                            responseModel = await _mediator.Send(request);
                            _logger.LogInformation("UploadInvoiceSettingsTemplate ended");
                        }
                    }
                    else
                    {
                        responseModel.Success = false;
                        responseModel.Message = "Invalid logged in user.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in UploadInvoiceSettingsTemplate . Error: {Error}", ex);
                responseModel.Success = false;
                responseModel.Message = "An error occurred while fetching the invoice settings." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(responseModel);
        }
    }
}
