using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Records one glucose reading. Computes ValueMgDl (unit conversion) and
    /// IsHypo/IsHyper server-side and echoes them back so the UI can flash a warning
    /// immediately. Pure insert, no transaction.</summary>
    public class GlucoseReadingCommandHandlers : IRequestHandler<RecordGlucoseReadingRequestModel, RecordGlucoseReadingResponseModel>
    {
        private readonly AppDbContext _context;

        public GlucoseReadingCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordGlucoseReadingResponseModel> Handle(RecordGlucoseReadingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordGlucoseReadingResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var unit = string.IsNullOrWhiteSpace(request.Unit) ? IpdConstants.GlucoseUnit.MgDl : request.Unit.Trim();
                if (!IpdConstants.GlucoseUnit.All.Contains(unit))
                    return new RecordGlucoseReadingResponseModel { Success = false, Message = "Invalid glucose unit." };

                if (request.Value <= 0)
                    return new RecordGlucoseReadingResponseModel { Success = false, Message = "Value must be greater than 0." };

                if (request.InsulinGiven && (!request.InsulinUnits.HasValue || request.InsulinUnits.Value <= 0))
                    return new RecordGlucoseReadingResponseModel { Success = false, Message = "Insulin units are required when insulin was given." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordGlucoseReadingResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new RecordGlucoseReadingResponseModel { Success = false, Message = "Admission is not active." };

                var valueMgDl = unit == IpdConstants.GlucoseUnit.MmolL
                    ? Math.Round(request.Value * IpdConstants.GlucoseUnit.MmolLToMgDlFactor, 2)
                    : request.Value;
                var isHypo = valueMgDl < IpdConstants.GlucoseThresholds.HypoMgDl;
                var isHyper = valueMgDl > IpdConstants.GlucoseThresholds.HyperMgDl;

                var now = DateTime.UtcNow;
                var reading = new GlucoseReading
                {
                    GlucoseReadingId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    Value = request.Value,
                    Unit = unit,
                    ValueMgDl = valueMgDl,
                    Method = string.IsNullOrWhiteSpace(request.Method) ? null : request.Method.Trim(),
                    MealTag = string.IsNullOrWhiteSpace(request.MealTag) ? null : request.MealTag.Trim().ToUpperInvariant(),
                    InsulinGiven = request.InsulinGiven,
                    InsulinUnits = request.InsulinGiven ? request.InsulinUnits : null,
                    InsulinType = request.InsulinGiven ? request.InsulinType?.Trim() : null,
                    InsulinRoute = request.InsulinGiven ? request.InsulinRoute?.Trim() : null,
                    IsHypo = isHypo,
                    IsHyper = isHyper,
                    RecordedAt = now,
                    RecordedBy = request.LoggedInUserName,
                    RecordedByUserId = request.LoggedInUserId,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.GlucoseReading.Add(reading);

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordGlucoseReadingResponseModel
                {
                    Success = true,
                    Message = "Glucose reading recorded.",
                    GlucoseReadingId = reading.GlucoseReadingId,
                    ValueMgDl = valueMgDl,
                    IsHypo = isHypo,
                    IsHyper = isHyper,
                };
            }
            catch (Exception)
            {
                return new RecordGlucoseReadingResponseModel { Success = false, Message = "Error recording glucose reading." };
            }
        }
    }
}
