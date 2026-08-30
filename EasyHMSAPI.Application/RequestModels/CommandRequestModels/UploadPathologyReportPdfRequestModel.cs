using MediatR;
using Microsoft.AspNetCore.Http;
using System;
using System.Text.Json.Serialization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UploadPathologyReportPdfRequestModel : IRequest<UploadPathologyReportPdfResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid ReportId { get; set; }
        public IFormFile? File { get; set; }
    }
}
