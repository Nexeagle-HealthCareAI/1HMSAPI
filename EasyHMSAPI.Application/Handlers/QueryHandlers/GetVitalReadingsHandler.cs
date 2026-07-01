using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Vitals trend for an admission — defaults to the last 7 days when no window is given.</summary>
    public class GetVitalReadingsHandler : IRequestHandler<GetVitalReadingsRequestModel, GetVitalReadingsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetVitalReadingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetVitalReadingsResponseModel> Handle(GetVitalReadingsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetVitalReadingsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var toUtc = request.ToUtc ?? DateTime.UtcNow;
                var fromUtc = request.FromUtc ?? toUtc.AddDays(-7);

                var readings = await _context.VitalReading
                    .Where(v => v.HospitalId == request.HospitalId && v.AdmissionId == request.AdmissionId
                        && v.RecordedAt >= fromUtc && v.RecordedAt <= toUtc)
                    .OrderByDescending(v => v.RecordedAt)
                    .Select(v => new VitalReadingItem
                    {
                        VitalReadingId = v.VitalReadingId,
                        RecordedAt = v.RecordedAt,
                        RecordedBy = v.RecordedBy,
                        Temperature = v.Temperature,
                        TemperatureUnit = v.TemperatureUnit,
                        Pulse = v.Pulse,
                        SystolicBP = v.SystolicBP,
                        DiastolicBP = v.DiastolicBP,
                        RespiratoryRate = v.RespiratoryRate,
                        SpO2 = v.SpO2,
                        WeightKg = v.WeightKg,
                        HeightCm = v.HeightCm,
                        BMI = v.BMI,
                        GcsEye = v.GcsEye,
                        GcsVerbal = v.GcsVerbal,
                        GcsMotor = v.GcsMotor,
                        GcsTotal = v.GcsTotal,
                        PainScore = v.PainScore,
                        Notes = v.Notes,
                    })
                    .ToListAsync(cancellationToken);

                return new GetVitalReadingsResponseModel { Success = true, Readings = readings };
            }
            catch (Exception)
            {
                return new GetVitalReadingsResponseModel { Success = false, Message = "Error loading vital readings." };
            }
        }
    }
}
