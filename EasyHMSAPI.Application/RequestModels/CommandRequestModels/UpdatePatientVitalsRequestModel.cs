using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdatePatientVitalsRequestModel : IRequest<UpdatePatientVitalsResponseModel>
    {
        [Required]
        public Guid AppointmentId { get; set; }
        [Required]
        public string? PatientId { get; set; }
        [Required]
        public VitalsJson? VitalsJson { get; set; }
        [Required]
        public Guid RecordedBy { get; set; }
    }

    public class VitalsJson
    {
        public BloodPressure? Bp { get; set; }
        public decimal? Pulse { get; set; }
        public decimal? TempC { get; set; }
        public decimal? Spo2 { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? Bmi { get; set; }
        public decimal? RespiratoryRate { get; set; }
    }

    public class BloodPressure
    {
        public decimal? Sys { get; set; }
        public decimal? Dia { get; set; }
    }
}
