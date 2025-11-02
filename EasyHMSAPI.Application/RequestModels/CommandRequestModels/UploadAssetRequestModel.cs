using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadAssetRequestModel : IRequest<UploadAssetResponseModel>
    {
        [Required]
        public IFormFile? File { get; set; }
        [Required]
        public Guid DoctorId { get; set; }
        [Required]
        public Guid PrescriptionSettingId { get; set; }
        [Required]
        public string? AssetType { get; set; }
    }
}
