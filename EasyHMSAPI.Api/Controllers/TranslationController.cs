using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Api.Common;

namespace EasyHMSAPI.Api.Controllers
{
    [ApiController]
    [Route("api/v1/translation")]
    [Authorize]
    [SkipHospitalAccessCheck]
    public class TranslationController : ControllerBase
    {
        private readonly ITranslationService _translationService;

        public TranslationController(ITranslationService translationService)
        {
            _translationService = translationService;
        }

        public class TranslateRequest
        {
            public string Text { get; set; }
            public string TargetLanguage { get; set; }
        }

        public class TranslateMultipleRequest
        {
            public Dictionary<string, string> Texts { get; set; }
            public string TargetLanguage { get; set; }
        }

        [HttpPost("translate")]
        public async Task<IActionResult> Translate([FromBody] TranslateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Text))
                return BadRequest("Text is required");

            var result = await _translationService.TranslateTextAsync(request.Text, request.TargetLanguage ?? "Hindi");
            return Ok(new { translatedText = result });
        }

        [HttpPost("translate-multiple")]
        public async Task<IActionResult> TranslateMultiple([FromBody] TranslateMultipleRequest request)
        {
            if (request?.Texts == null || request.Texts.Count == 0)
                return BadRequest("Texts are required");

            var result = await _translationService.TranslateMultipleAsync(request.Texts, request.TargetLanguage ?? "Hindi");
            return Ok(result);
        }
    }
}
