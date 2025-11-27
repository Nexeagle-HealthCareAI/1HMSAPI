using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

        [HttpGet("patient-details/lookup-data")]
        [Authorize]
        public async Task<IActionResult> GetLookupData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string lookupType)
        {
            _logger.LogInformation("GetLookupData started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", DateTime.UtcNow, hospitalId, doctorId, lookupType);
            if (hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(lookupType))
                return BadRequest(new { Message = "Invalid request parameters." });

            if (!AppConstants.LookupTypes.Contains(lookupType.ToUpper()))
                return BadRequest(new { Message = "Invalid lookup type." });

            var request = new GetPatientLookupDataRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctorId,
                LookupType = lookupType
            };

            var result = await _mediator.Send(request);

            _logger.LogInformation("GetLookupData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);

            return Ok(result);
        }

        [HttpGet("configuration/preference-setting/doctorId={doctorId}&hospitalId={hospitalId}")]
        [Authorize]
        public async Task<IActionResult> GetDoctorPreferenceSetting(Guid doctorId, Guid hospitalId)
        {
            _logger.LogInformation("GetDoctorPreferenceSetting started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
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
            if (model == null || hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(lookupType))
                return BadRequest(new { Message = "Invalid request parameters." });

            if(!AppConstants.LookupTypes.Contains(lookupType.ToUpper()))
                return BadRequest(new { Message = "Invalid lookup type." });

            var request = new UpsertPersonalizedDataRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctorId,
                LookupType = lookupType,
                Data = model
            };
            var result = await _mediator.Send(request);

            _logger.LogInformation("UpsertPersonalizedData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);

            return Ok(result);
        }

        [HttpGet("configuration/personalized-data")]
        [Authorize]
        public async Task<IActionResult> GetPersonalizedData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string lookupType)
        {
            _logger.LogInformation("GetPersonalizedData started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", DateTime.UtcNow, hospitalId, doctorId, lookupType);
            if (hospitalId == Guid.Empty || doctorId == Guid.Empty || string.IsNullOrWhiteSpace(lookupType))
                return BadRequest(new { Message = "Invalid request parameters." });

            if (!AppConstants.LookupTypes.Contains(lookupType.ToUpper()))
                return BadRequest(new { Message = "Invalid lookup type." });

            var request = new GetPersonalizedDataRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctorId,
                LookupType = lookupType
            };
            var result = await _mediator.Send(request);

            _logger.LogInformation("GetPersonalizedData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, lookupType: {LookupType}", hospitalId, doctorId, lookupType);

            return Ok(result);
        }

        [HttpDelete("configuration/personalized-data")]
        [Authorize]
        public async Task<IActionResult> DeletePersonalizedData([FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] Guid personalId)
        {
            _logger.LogInformation("DeletePersonalizedData started at {Time} for hospitalId: {HospitalId}, doctorId: {DoctorId}, personalId: {PersonalId}", DateTime.UtcNow, hospitalId, doctorId, personalId);
            if (hospitalId == Guid.Empty || doctorId == Guid.Empty || personalId == Guid.Empty)
                return BadRequest(new { Message = "Invalid request parameters." });

            var request = new DeletePersonalizedDataRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PersonalId = personalId
            };
            var result = await _mediator.Send(request);

            _logger.LogInformation("DeletePersonalizedData ended for hospitalId: {HospitalId}, doctorId: {DoctorId}, personalId: {PersonalId}", hospitalId, doctorId, personalId);

            return Ok(result);
        }

        [HttpPut("configuration/personalized-medicine/doctorId={doctorId}&hospitalId={hospitalId}")]
        [Authorize]
        public async Task<IActionResult> UpsertPreferredMedicine(Guid doctorId, Guid hospitalId, [FromBody] PreferredMedicineDataModel model)
        {
            _logger.LogInformation("UpsertPreferredMedicine started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            if (model == null || doctorId == Guid.Empty || hospitalId == Guid.Empty)
                return BadRequest(new { Message = "Invalid request parameters." });

            var request = new UpsertPreferredMedicineRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                Medicine = model
            };
            var result = await _mediator.Send(request);
            _logger.LogInformation("UpsertPreferredMedicine ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);

            return Ok(result);
        }

        [HttpGet("configuration/personalized-medicine/doctorId={doctorId}&hospitalId={hospitalId}")]
        [Authorize]
        public async Task<IActionResult> GetPreferredMedicines(Guid doctorId, Guid hospitalId)
        {
            _logger.LogInformation("GetPreferredMedicines started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            if (doctorId == Guid.Empty || hospitalId == Guid.Empty)
                return BadRequest(new { Message = "Invalid doctorId or hospitalId." });

            var request = new GetPreferredMedicinesRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId
            };
            var result = await _mediator.Send(request);
            _logger.LogInformation("GetPreferredMedicines ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);

            return Ok(result);
        }
    }
}
