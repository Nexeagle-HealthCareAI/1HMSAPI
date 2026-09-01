using MediatR;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UpdatePathologyOrderCommand : IRequest<ResponseModels.CommandResponseModels.UpdatePathologyOrderResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public string PatientId { get; set; } = null!;
        public Guid? EncounterId { get; set; }
        public Guid? AdmissionId { get; set; }
        public string? SourceType { get; set; }
        public List<Guid> TestIds { get; set; } = new();
        public string? Notes { get; set; }
        public bool IsStat { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
