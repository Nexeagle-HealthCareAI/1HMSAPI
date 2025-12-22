using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MailKit.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("e-prescription")]
    public class EPrescriptionController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<EPrescriptionController> _logger;
        public EPrescriptionController(IMediator mediator, ILogger<EPrescriptionController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("patient-details/vitals")]
        [Authorize]
        public async Task<IActionResult> GetPatientVitals([FromQuery] string patientId, [FromQuery] Guid appointmentId)
        {
            _logger.LogInformation("GetPatientVitals started at {Time} for patientId: {PatientId}, appointmentId: {AppointmentId}", DateTime.UtcNow, patientId, appointmentId);
            if (string.IsNullOrEmpty(patientId) || appointmentId == Guid.Empty)
                return BadRequest("Invalid patientId or appointmentId.");

            var result = await _mediator.Send(new GetPatientVitalsRequestModel
            {
                PatientId = patientId.ToString(),
                AppointmentId = appointmentId
            });

            _logger.LogInformation("GetPatientVitals ended for patientId: {PatientId}, appointmentId: {AppointmentId}", patientId, appointmentId);

            return Ok(result);
        }

        [HttpGet("lookup/details")]
        [Authorize]
        public async Task<ActionResult<GetPatientLookupDataResponseModel>> GetLookupData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId)
        {
            _logger.LogInformation("GetLookupData started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            GetPatientLookupDataResponseModel response = new();
            try
            {
                if (hospitalId == Guid.Empty || doctorId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    GetPatientLookupDataRequestModel requestModel = new()
                    {
                        HospitalId = hospitalId,
                        DoctorId = doctorId
                    };
                    response = await _mediator.Send(requestModel);
                    _logger.LogInformation("GetLookupData ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLookupData for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }

            return response;
        }

        [HttpGet("lookup/search")]
        [Authorize]
        public async Task<ActionResult<SearchLookupDataResponseModel>> SearchLookupData([FromQuery] string lookupType, [FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string searchText)
        {
            _logger.LogInformation("SearchLookupData started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}, lookupType: {LookupType}, searchTerm: {SearchTerm}", DateTime.UtcNow, doctorId, hospitalId, lookupType, searchText);
            SearchLookupDataResponseModel response = new();
            try
            {
                if (hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(lookupType) || string.IsNullOrWhiteSpace(searchText))
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    SearchLookupDataRequestModel request = new()
                    {
                        HospitalId = hospitalId,
                        DoctorId = doctorId,
                        LookupType = lookupType,
                        SearchText = searchText
                    };
                    response = await _mediator.Send(request);
                    _logger.LogInformation("SearchLookupData ended for doctorId: {DoctorId}, hospitalId: {HospitalId}, lookupType: {LookupType}, searchTerm: {SearchTerm}", doctorId, hospitalId, lookupType, searchText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchLookupData for doctorId: {DoctorId}, hospitalId: {HospitalId}, lookupType: {LookupType}, searchTerm: {SearchTerm}", doctorId, hospitalId, lookupType, searchText);
                response.Success = false;
                response.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }
           
            return Ok(response);
        }

        [HttpGet("configuration/preference-setting/doctorId={doctorId}&hospitalId={hospitalId}")]
        [Authorize]
        public async Task<IActionResult> GetDoctorPreferenceSetting(Guid doctorId, Guid hospitalId)
        {
            _logger.LogInformation("GetDoctorPreferenceSetting started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            if (doctorId == Guid.Empty || hospitalId == Guid.Empty)
                return BadRequest(new { Message = "Invalid doctorId or hospitalId." });

            if (!await ValidateDoctorHospitalAsync(hospitalId, doctorId, HttpContext.RequestAborted))
                return BadRequest(new { Message = "Doctor is not associated with the specified hospital." });
            var result = await _mediator.Send(new GetDoctorPreferenceSettingRequestModel { DoctorId = doctorId, HospitalId = hospitalId });
            
            _logger.LogInformation("GetDoctorPreferenceSetting ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);

            return Ok(result);
        }

        [HttpPut("configuration/update-preference-setting/doctorId={doctorId}&hospitalId={hospitalId}")]
        [Authorize]
        public async Task<IActionResult> UpdateDoctorPreferenceSetting(Guid doctorId, Guid hospitalId, [FromBody] DoctorSectionPreferenceUpdateModel model)
        {
            _logger.LogInformation("UpdateDoctorPreferenceSetting started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            if (model == null)
                return BadRequest("Invalid request body.");
            if (doctorId == Guid.Empty || hospitalId == Guid.Empty)
                return BadRequest(new { Message = "Invalid doctorId or hospitalId." });

            if (!await ValidateDoctorHospitalAsync(hospitalId, doctorId, HttpContext.RequestAborted))
                return BadRequest(new { Message = "Doctor is not associated with the specified hospital." });
            model.DoctorId = doctorId;
            model.HospitalId = hospitalId;
            var request = new UpdateDoctorPreferenceSettingRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                Preference = model
            };
            var result = await _mediator.Send(request);
            _logger.LogInformation("UpdateDoctorPreferenceSetting ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);

            return Ok(result);
        }

        [HttpPut("configuration/personalized-data")]
        [Authorize]
        public async Task<IActionResult> UpsertPersonalizedData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string lookupType, [FromBody] PersonalizedLookupDataModel model)
        {
            _logger.LogInformation("UpsertPersonalizedData started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", DateTime.UtcNow, hospitalId, doctorId, lookupType);
            UpsertPersonalizedDataResponseModel response = new();
            try
            {
                if (model == null || hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(lookupType))
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";

                }
                else if(string.IsNullOrEmpty(model.Name) || string.IsNullOrWhiteSpace(model.Name))
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    var userIdClaim = User.FindFirst("userId")?.Value;
                    if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
                    {
                        UpsertPersonalizedDataRequestModel request = new()
                        {
                            HospitalId = hospitalId,
                            DoctorId = doctorId,
                            LookupType = lookupType,
                            Data = model,
                            LoggedInUserId = userId
                        };

                        if (request.LoggedInUserId == Guid.Empty)
                        {
                            response.Success = false;
                            response.Message = "Invalid logged in user.";
                        }
                        else
                        {
                            request.HospitalId = hospitalId;
                            request.DoctorId = doctorId;
                            request.LookupType = lookupType;

                            response = await _mediator.Send(request);
                            _logger.LogInformation("UpsertPersonalizedData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);
                        }
                    }
                    else
                    {
                        response.Success = false;
                        response.Message = "Invalid logged in user.";
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertPersonalizedData for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);
                response.Success = false;
                response.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }

        [HttpGet("configuration/personalized-data")]
        [Authorize]
        public async Task<ActionResult<GetPersonalizedDataResponseModel>> GetPersonalizedData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string lookupType)
        {
            _logger.LogInformation("GetPersonalizedData started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", DateTime.UtcNow, hospitalId, doctorId, lookupType);
            GetPersonalizedDataResponseModel response = new();
            try
            {
                if (hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(lookupType))
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    var request = new GetPersonalizedDataRequestModel
                    {
                        HospitalId = hospitalId,
                        DoctorId = doctorId,
                        LookupType = lookupType
                    };
                    response = await _mediator.Send(request);

                    _logger.LogInformation("GetPersonalizedData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPersonalizedData for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);
                response.Success = false;
                response.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }

        [HttpDelete("configuration/personalized-data")]
        [Authorize]
        public async Task<ActionResult<DeletePersonalizedDataResponseModel>> DeletePersonalizedData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] Guid personalId)
        {
            _logger.LogInformation("DeletePersonalizedData started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}, personalId: {PersonalId}", DateTime.UtcNow, hospitalId, doctorId, personalId);
            DeletePersonalizedDataResponseModel response = new();
            try
            {
                if (hospitalId == Guid.Empty || doctorId == Guid.Empty || personalId == Guid.Empty)
                {
                    response.Success = false;
                    response.Message = "Invalid request parameters.";
                }
                else
                {
                    var request = new DeletePersonalizedDataRequestModel
                    {
                        HospitalId = hospitalId,
                        DoctorId = doctorId,
                        PersonalId = personalId
                    };
                    response = await _mediator.Send(request);

                    _logger.LogInformation("DeletePersonalizedData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, personalId: {PersonalId}", hospitalId, doctorId, personalId);
                }
            }
            catch(Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(response);
        }

        [HttpPost("attachments/upload")]
        [Authorize]
        public async Task<ActionResult<UploadPrescriptionAttachmentsResponseModel>> UploadAttachment(UploadPrescriptionAttachmentsRequestModel request)
        {
            _logger.LogInformation("UploadAttachment started at {Time} for appointmentId: {AppointmentId}, patientId: {PatientId}, hospitalId: {HospitalId}, doctorId: {DoctorId}", DateTime.UtcNow, request.AppointmentId, request.PatientId, request.HospitalId, request.DoctorId);
            UploadPrescriptionAttachmentsResponseModel result = new();
            try
            {
                if (request == null || request.AppointmentId == Guid.Empty || string.IsNullOrEmpty(request.PatientId) || request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty || request.File == null || string.IsNullOrEmpty(request.Notes))
                {
                    result.Success = false;
                    result.Message = "Invalid request parameters.";
                }
                else
                {
                    var userIdClaim = User.FindFirst("userId")?.Value;
                    if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
                    {
                        request.LoggedInUserId = userId;

                        if (request.LoggedInUserId == Guid.Empty)
                        {
                            result.Success = false;
                            result.Message = "Invalid logged in user.";
                        }
                        else
                        {
                            result =  await  _mediator.Send(request);
                            _logger.LogInformation("UploadAttachment ended for appointmentId: {AppointmentId}, patientId: {PatientId}", request.AppointmentId, request.PatientId);
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Invalid logged in user.";
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error in UploadAttachment for appointmentId: {AppointmentId}, patientId: {PatientId}", request.AppointmentId, request.PatientId);
                result.Success = false;
                result.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(result);
        }
        [HttpGet("attachments/list")]
        [Authorize]
        public async Task<ActionResult<GetPrescriptionAttachmentsResponseModel>> GetAttachments([FromQuery] Guid appointmentId, Guid hospitalId, Guid doctorId, [FromQuery] string patientId)
        {
            _logger.LogInformation("GetAttachments started at {Time} for appointmentId: {AppointmentId}, patientId: {PatientId}", DateTime.UtcNow, appointmentId, patientId);
            GetPrescriptionAttachmentsResponseModel result = new();
            try
            {
                if (appointmentId == Guid.Empty || hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrEmpty(patientId))
                {
                    result.Success = false;
                    result.Message = "Invalid request parameters.";
                }
                else
                {
                    GetPrescriptionAttachmentsRequestModel requestModel = new()
                    {
                        AppointmentId = appointmentId,
                        PatientId = patientId,
                        HospitalId = hospitalId,
                        DoctorId = doctorId
                    };
                    result = await _mediator.Send(requestModel);
                    _logger.LogInformation("GetAttachments ended at {Time} for appointmentId: {AppointmentId}, patientId: {PatientId}", DateTime.UtcNow, appointmentId, patientId);
                }
                
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error in GetAttachments for appointmentId: {AppointmentId}, patientId: {PatientId}", appointmentId, patientId);
                result.Success = false;
                result.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(result);
        }
        [HttpDelete("attachments/delete")]
        [Authorize]
        public async Task<ActionResult<DeletePrescriptionAttachmentResponseModel>> DeleteAttachment([FromQuery] DeletePrescriptionAttachmentRequestModel request)
        {
            _logger.LogInformation("DeleteAttachment started at {Time} for attachmentId: {AttachmentId}", DateTime.UtcNow, request.AttachmentId);
            DeletePrescriptionAttachmentResponseModel result = new();

            if (request.AttachmentId == Guid.Empty)
            {
                result.Success = false;
                result.Message = "Invalid request parameters.";
            }
            else
            {
                result = await _mediator.Send(request);
                _logger.LogInformation("DeleteAttachment ended for attachmentId: {AttachmentId}", request.AttachmentId);
            }
           
            return Ok(result);
        }

        [HttpPost("generate-prescription-details")]
        [Authorize]
        public async Task<IActionResult> GeneratePrescription([FromBody] GeneratePrescriptionRequestModel request)
        {
            _logger.LogInformation("GeneratePrescription started at {Time} for appointmentId: {AppointmentId}, patientId: {PatientId}, hospitalId: {HospitalId}, doctorId: {DoctorId}", DateTime.UtcNow, request.AppointmentId, request.PatientId, request.HospitalId, request.DoctorId);

            if (request == null || request.AppointmentId == Guid.Empty || string.IsNullOrEmpty(request.PatientId) || request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty)
            {
                return BadRequest(new { Message = "Invalid request parameters." });
            }

            if (!await ValidateDoctorHospitalAsync(request.HospitalId, request.DoctorId, HttpContext.RequestAborted))
                return BadRequest(new { Message = "Doctor is not associated with the specified hospital." });
            
            var result = await _mediator.Send(request);

            _logger.LogInformation("GeneratePrescription ended for appointmentId: {AppointmentId}, patientId: {PatientId}", request.AppointmentId, request.PatientId);

            return Ok(result);
        }

        private async Task<bool> ValidateDoctorHospitalAsync(Guid hospitalId, Guid doctorId, CancellationToken ct)
        {
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            // Ensure the doctor exists and is linked to the hospital through HospitalUsers
            var doctor = await db.Doctors
                .Where(d => d.DoctorID == doctorId)
                .Select(d => new { d.UserID })
                .FirstOrDefaultAsync(ct);
            if (doctor == null) return false;

            var isLinked = await db.HospitalUsers
                .AnyAsync(hu => hu.HospitalID == hospitalId && hu.UserID == doctor.UserID, ct);

            return isLinked;
        }
    }
}
