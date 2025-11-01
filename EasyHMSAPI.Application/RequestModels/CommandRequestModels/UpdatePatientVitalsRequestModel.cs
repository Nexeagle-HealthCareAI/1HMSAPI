using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
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
        public short? Pulse { get; set; }
        public decimal? TempC { get; set; }
        public decimal? Spo2 { get; set; }
        public short? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? Bmi { get; set; }
    }

    public class BloodPressure
    {
        public short? Sys { get; set; }
        public short? Dia { get; set; }
    }
}
