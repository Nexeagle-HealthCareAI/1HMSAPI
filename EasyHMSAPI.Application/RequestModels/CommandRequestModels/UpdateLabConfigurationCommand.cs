using MediatR;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateLabConfigurationCommand : IRequest<bool>
    {
        [Required]
        public Guid HospitalId { get; set; }

        public bool AutoBillOnOrder { get; set; }
        public string? DefaultReportHeaderBlob { get; set; }
        public string? DefaultReportFooterText { get; set; }
        public string? LetterheadMode { get; set; }
        public string? ReportFieldLayoutJson { get; set; }

        public string? LabName { get; set; }
        public string? LabAddress { get; set; }
        public string? LabRegistrationNumber { get; set; }
        public string? TechnicianName { get; set; }
        public string? PathologistName { get; set; }

        public bool IsPubliclyListed { get; set; }
        public string? PublicDescription { get; set; }
        public string? PublicContactPhone { get; set; }
        public string? PublicContactEmail { get; set; }
        public string? LabCity { get; set; }
        public string? LabState { get; set; }
        public string? LabPincode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? TestCategoriesJson { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
