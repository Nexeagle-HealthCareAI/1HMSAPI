using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("patient")]
    [ApiController]
    public class PatientController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AppointmentsController> _logger;
        public PatientController(IMediator mediator, ILogger<AppointmentsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        [Route("hospitalId={hospitalId}")]
        public async Task<ActionResult<GetPatientsByHospitalIdResponseModel>> GetPatientsByHospitalIdAsync(Guid hospitalId)
        {
            GetPatientsByHospitalIdResponseModel result = new();
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    result.Success = false;
                    result.Message = "Invalid HospitalId provided.";
                }
                else
                {
                    GetPatientsByHospitalIdRequestModel request = new()
                    {
                        HospitalId = hospitalId
                    };
                    result = await _mediator.Send(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching patients for HospitalId: {HospitalId}", hospitalId);
                result.Success = false;
                result.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(result);
        }
    }
}
