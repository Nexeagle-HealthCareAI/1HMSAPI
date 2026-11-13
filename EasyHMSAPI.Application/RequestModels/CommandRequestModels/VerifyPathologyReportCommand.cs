using System.Text.Json.Serialization;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Pathologist verification (sign-off) of a generated report. Re-required after any amendment.</summary>
    public class VerifyPathologyReportCommand : IRequest<VerifyPathologyReportResult>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public Guid ReportId { get; set; }
        public string? PathologistName { get; set; }
        public string? PathologistRegNo { get; set; }
        public Guid? PathologistDoctorId { get; set; }

        [JsonIgnore] public string? LoggedInUserName { get; set; }
        [JsonIgnore] public Guid LoggedInUserId { get; set; }
    }

    public class VerifyPathologyReportResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
