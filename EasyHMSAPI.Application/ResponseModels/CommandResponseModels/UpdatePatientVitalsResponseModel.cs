using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UpdatePatientVitalsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid VitalId { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid? RecordedBy { get; set; }
    }
}
