using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VentilatorSettingsCommandHandlers : IRequestHandler<RecordVentilatorSettingsRequestModel, RecordVentilatorSettingsResponseModel>
    {
        private readonly AppDbContext _context;

        public VentilatorSettingsCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordVentilatorSettingsResponseModel> Handle(RecordVentilatorSettingsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordVentilatorSettingsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (string.IsNullOrWhiteSpace(request.Mode))
                    return new RecordVentilatorSettingsResponseModel { Success = false, Message = "Ventilator mode is required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordVentilatorSettingsResponseModel { Success = false, Message = "Admission not found." };

                var now = DateTime.UtcNow;
                var settings = new VentilatorSettings
                {
                    VentilatorSettingsId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    Mode = request.Mode.Trim().ToUpperInvariant(),
                    FiO2Percent = request.FiO2Percent,
                    PeepCmH2o = request.PeepCmH2o,
                    TidalVolumeMl = request.TidalVolumeMl,
                    RespiratoryRateSet = request.RespiratoryRateSet,
                    PeakInspiratoryPressure = request.PeakInspiratoryPressure,
                    PlateauPressure = request.PlateauPressure,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    ScoredBy = request.LoggedInUserName ?? "Unknown",
                    ScoredAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.VentilatorSettings.Add(settings);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordVentilatorSettingsResponseModel { Success = true, Message = "Ventilator settings recorded.", VentilatorSettingsId = settings.VentilatorSettingsId };
            }
            catch (Exception)
            {
                return new RecordVentilatorSettingsResponseModel { Success = false, Message = "Error recording ventilator settings." };
            }
        }
    }
}
