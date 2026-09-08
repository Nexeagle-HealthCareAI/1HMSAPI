using System;
using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: ExternalLabId present => update that lab in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class UpsertPathologyExternalLabRequestModel : IRequest<UpsertPathologyExternalLabResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? ExternalLabId { get; set; }
        public string LabName { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? AccreditationNo { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
