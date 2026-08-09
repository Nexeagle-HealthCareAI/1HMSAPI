using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("medicines")]
    [ApiController]
    public class MedicinesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<MedicinesController> _logger;
        public MedicinesController(IMediator mediator, ILogger<MedicinesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPut("doctor-preference")]
        [Authorize]
        public async Task<ActionResult<UpsertPreferredMedicineResponseModel>> UpsertPreferredMedicine([FromBody] UpsertPreferredMedicineRequestModel request)
        {
            _logger.LogInformation("UpsertPreferredMedicine started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, request.DoctorId, request.HospitalId);
            UpsertPreferredMedicineResponseModel response = new();
            try
            {
                if (request.DoctorId == Guid.Empty || request.HospitalId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    var userIdClaim = User.FindFirst("userId")?.Value;
                    if (userIdClaim == null)
                    {
                        response.Success = false;
                        response.Message = "User is not authenticated.";
                    }
                    else if(Guid.TryParse(userIdClaim, out var userId))
                    {
                        request.LoggedInUserId = userId;

                        if (request.LoggedInUserId == Guid.Empty)
                        {
                            response.Success = false;
                            response.Message = "Invalid logged in user.";
                        }
                        else
                        {
                            response = await _mediator.Send(request);
                            _logger.LogInformation("UpsertPreferredMedicine ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);
                        }
                    }
                } 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertPreferredMedicine for doctorId: {DoctorId}, hospitalId: {HospitalId}" + ex.Message + ex.InnerException + ex.StackTrace, request.DoctorId, request.HospitalId);
                response.Success = false;
                response.Message = "An error occurred while processing the request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }

        [HttpGet("doctor-preference/doctorId={doctorId}&hospitalId={hospitalId}")]
        [Authorize]
        public async Task<ActionResult<GetPreferredMedicinesResponseModel>> GetPreferredMedicines(Guid doctorId, Guid hospitalId)
        {
            _logger.LogInformation("GetPreferredMedicines started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            GetPreferredMedicinesResponseModel response = new();
            try
            {
                if (doctorId == Guid.Empty || hospitalId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid doctorId or hospitalId.";
                }
                else
                {
                    var request = new GetPreferredMedicinesRequestModel
                    {
                        DoctorId = doctorId,
                        HospitalId = hospitalId
                    };

                    response = await _mediator.Send(request);
                    _logger.LogInformation("GetPreferredMedicines ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);
                }  
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPreferredMedicines for doctorId: {DoctorId}, hospitalId: {HospitalId}" + ex.Message + ex.InnerException + ex.StackTrace, doctorId, hospitalId);
                response.Success = false;
                response.Message = "An error occurred while processing the request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }

        [HttpDelete("doctor-preference")]
        [Authorize]
        public async Task<ActionResult<DeletePreferredMedicineResponseModel>> DeletePreferredMedicine(DeletePreferredMedicineRequestModel request)
        {
            _logger.LogInformation("DeletePreferredMedicine started at {Time} for preferredId: {PreferredId}, doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, request.PreferredId, request.DoctorId, request.HospitalId);
            DeletePreferredMedicineResponseModel response = new();
            try
            {
                if (request.PreferredId <= 0 || request.DoctorId == Guid.Empty || request.HospitalId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid preferredId, doctorId or hospitalId.";
                }
                else
                {
                    response = await _mediator.Send(request);
                    _logger.LogInformation("DeletePreferredMedicine ended for preferredId: {PreferredId}, doctorId: {DoctorId}, hospitalId: {HospitalId}", request.PreferredId, request.DoctorId, request.HospitalId);
                }
            }
            catch(Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred while processing the request." + ex.Message + ex.InnerException + ex.StackTrace;
                _logger.LogError(ex, "Error in DeletePreferredMedicine for preferredId: {PreferredId}, doctorId: {DoctorId}, hospitalId: {HospitalId}" + ex.Message + ex.InnerException + ex.StackTrace, request.PreferredId, request.DoctorId, request.HospitalId);
            }
            
            return Ok(response);
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<ActionResult<SearchMedicinesResponseModel>> GetMedicineSuggestions([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string searchText)
        {
            _logger.LogInformation("SearchMedicines started at {Time} for doctorId: {doctorId}, hospitalId: {hospitalId}, searchText: {searchText}", DateTime.UtcNow, doctorId, hospitalId, searchText);
            SearchMedicinesResponseModel response = new();
            try
            {
                if (hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(searchText) || string.IsNullOrEmpty(searchText))
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    SearchMedicinesRequestModel request = new()
                    {
                        HospitalId = hospitalId,
                        DoctorId = doctorId,
                        SearchText = searchText
                    };
                    response = await _mediator.Send(request);
                    _logger.LogInformation("SearchLookupData ended for doctorId: {doctorId}, hospitalId: {hospitalId}, searchText: {searchText}", doctorId, hospitalId, searchText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in SearchMedicines" + ex.Message + ex.InnerException + ex.StackTrace);
                response.Success = false;
                response.Message = "An error occurred while processing the request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }

        [HttpGet("{medicineId}/info")]
        [Authorize]
        public async Task<ActionResult<GetMedicineInfoResponseModel>> GetMedicineInfo(int medicineId)
        {
            _logger.LogInformation("GetMedicineInfo started at {Time} for medicineId: {MedicineId}", DateTime.UtcNow, medicineId);
            GetMedicineInfoResponseModel response = new();
            try
            {
                if (medicineId <= 0)
                {
                    response.Success = false;
                    response.Message = "Invalid medicineId.";
                }
                else
                {
                    GetMedicineInfoRequestModel request = new() { MedicineId = medicineId };
                    response = await _mediator.Send(request);
                    _logger.LogInformation("GetMedicineInfo ended for medicineId: {MedicineId}", medicineId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMedicineInfo for medicineId: {MedicineId}" + ex.Message + ex.InnerException + ex.StackTrace, medicineId);
                response.Success = false;
                response.Message = "An error occurred while processing the request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }
    }
}
