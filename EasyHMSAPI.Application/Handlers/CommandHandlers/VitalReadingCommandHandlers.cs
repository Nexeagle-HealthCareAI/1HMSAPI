using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Records one IPD vital-signs reading. Pure insert, no transaction (no cross-entity
    /// invariant to protect). BMI and GcsTotal are always server-computed, never client-supplied.
    /// </summary>
    public class VitalReadingCommandHandlers : IRequestHandler<RecordVitalReadingRequestModel, RecordVitalReadingResponseModel>
    {
        private readonly AppDbContext _context;

        public VitalReadingCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordVitalReadingResponseModel> Handle(RecordVitalReadingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordVitalReadingResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var hasAnyValue = request.Temperature.HasValue || request.Pulse.HasValue || request.SystolicBP.HasValue
                    || request.DiastolicBP.HasValue || request.RespiratoryRate.HasValue || request.SpO2.HasValue
                    || request.WeightKg.HasValue || request.HeightCm.HasValue || request.GcsEye.HasValue
                    || request.GcsVerbal.HasValue || request.GcsMotor.HasValue || request.PainScore.HasValue;
                if (!hasAnyValue)
                    return new RecordVitalReadingResponseModel { Success = false, Message = "At least one vital value is required." };

                var tempUnit = string.IsNullOrWhiteSpace(request.TemperatureUnit) ? null : request.TemperatureUnit.Trim().ToUpperInvariant();
                if (tempUnit != null && !IpdConstants.VitalTemperatureUnit.All.Contains(tempUnit))
                    return new RecordVitalReadingResponseModel { Success = false, Message = "Invalid temperature unit." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordVitalReadingResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new RecordVitalReadingResponseModel { Success = false, Message = "Admission is not active." };

                decimal? bmi = null;
                if (request.WeightKg.HasValue && request.HeightCm.HasValue && request.HeightCm.Value > 0)
                {
                    var heightM = request.HeightCm.Value / 100m;
                    bmi = Math.Round(request.WeightKg.Value / (heightM * heightM), 2);
                }

                int? gcsTotal = request.GcsEye.HasValue && request.GcsVerbal.HasValue && request.GcsMotor.HasValue
                    ? request.GcsEye.Value + request.GcsVerbal.Value + request.GcsMotor.Value
                    : null;

                var now = DateTime.UtcNow;
                var reading = new VitalReading
                {
                    VitalReadingId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    RecordedAt = now,
                    RecordedBy = request.LoggedInUserName,
                    RecordedByUserId = request.LoggedInUserId,
                    Temperature = request.Temperature,
                    TemperatureUnit = tempUnit,
                    Pulse = request.Pulse,
                    SystolicBP = request.SystolicBP,
                    DiastolicBP = request.DiastolicBP,
                    RespiratoryRate = request.RespiratoryRate,
                    SpO2 = request.SpO2,
                    WeightKg = request.WeightKg,
                    HeightCm = request.HeightCm,
                    BMI = bmi,
                    GcsEye = request.GcsEye,
                    GcsVerbal = request.GcsVerbal,
                    GcsMotor = request.GcsMotor,
                    GcsTotal = gcsTotal,
                    PainScore = request.PainScore,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.VitalReading.Add(reading);

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordVitalReadingResponseModel { Success = true, Message = "Vital reading recorded.", VitalReadingId = reading.VitalReadingId };
            }
            catch (Exception)
            {
                return new RecordVitalReadingResponseModel { Success = false, Message = "Error recording vital reading." };
            }
        }
    }
}
