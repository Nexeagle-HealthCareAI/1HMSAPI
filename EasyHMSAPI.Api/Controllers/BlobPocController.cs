using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("blob-poc")]
    [ApiController]
    public class BlobPocController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<UserController> _logger;
        private readonly string _containerName;
        private readonly IBlobStorageService _blobService;
        private readonly AppDbContext _context;

        public BlobPocController(IMediator mediator, ILogger<UserController> logger, IConfiguration configuration, IBlobStorageService blobService, AppDbContext context)
        {
            _mediator = mediator;
            _logger = logger;
            _containerName = "prescriptiontemplate";
            _blobService = blobService;
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadBlobPoc(Guid userId, IFormFile selectedFile)
        {
            _logger.LogInformation("BlobPocController UploadBlobPoc started at {Time}", DateTime.UtcNow);
            try
            {
                var c = new CancellationToken();
                // Placeholder for blob upload logic
                _logger.LogInformation("BlobPocController UploadBlobPoc ended");
                var url = await _blobService.UploadAsync(userId, selectedFile, _containerName, c);
                return Ok(new { Url = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BlobPocController UploadBlobPoc");
                return StatusCode(500, new { Message = "An error occurred while uploading the blob", Error = ex.Message });
            }
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadBlobPoc(Guid userId)
        {
            _logger.LogInformation("BlobPocController DownloadBlobPoc started at {Time}", DateTime.UtcNow);
            try
            {
                var c = new CancellationToken();
                // Placeholder for blob download logic
                var url = await _blobService.GetUrlAsync(userId, _containerName, c);
                _logger.LogInformation("BlobPocController DownloadBlobPoc ended");
                return Ok(new { Url = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BlobPocController DownloadBlobPoc");
                return StatusCode(500, new { Message = "An error occurred while downloading the blob", Error = ex.Message });
            }
        }
    }
}
