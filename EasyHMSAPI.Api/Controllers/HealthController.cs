using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Lightweight, unauthenticated reachability probe for the web client's connectivity
    /// heartbeat (navigator.onLine lies). No DB access — just confirms the API is reachable.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("health")]
    [AllowAnonymous]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [HttpHead]
        public IActionResult Get() => Ok(new { status = "ok", utc = DateTime.UtcNow });
    }
}
