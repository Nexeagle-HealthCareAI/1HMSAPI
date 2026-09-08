using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<TranslationController> _logger;

        public TranslationController(ITranslationService translationService, ILogger<TranslationController> logger)
        {
            _translationService = translationService;
            _logger = logger;
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

            try
            {
                var result = await _translationService.TranslateTextAsync(request.Text, request.TargetLanguage ?? "Hindi");
                return Ok(new { translatedText = result });
            }
            catch (Exception ex)
            {
                // Deliberately does NOT fall back to returning the original text disguised as a
                // translation -- unlike other Groq-backed features in this codebase, where a
                // deterministic fallback narrative is safe because the underlying numbers are
                // unchanged, silently handing back untranslated English here would look like a
                // successful translation to a patient who can't read English. Fail loud instead.
                _logger.LogError(ex, "Error in TranslationController.Translate");
                return StatusCode(500, new { Message = "Translation is temporarily unavailable. Please try again." });
            }
        }

        [HttpPost("translate-multiple")]
        public async Task<IActionResult> TranslateMultiple([FromBody] TranslateMultipleRequest request)
        {
            if (request?.Texts == null || request.Texts.Count == 0)
                return BadRequest("Texts are required");

            try
            {
                var result = await _translationService.TranslateMultipleAsync(request.Texts, request.TargetLanguage ?? "Hindi");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TranslationController.TranslateMultiple");
                return StatusCode(500, new { Message = "Translation is temporarily unavailable. Please try again." });
            }
        }
    }
}
