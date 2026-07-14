using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class EarlyWarningScoreCommandHandlers : IRequestHandler<RecordEarlyWarningScoreRequestModel, RecordEarlyWarningScoreResponseModel>
    {
        private readonly AppDbContext _context;

        public EarlyWarningScoreCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordEarlyWarningScoreResponseModel> Handle(RecordEarlyWarningScoreRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordEarlyWarningScoreResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var consciousness = request.ConsciousnessLevel?.Trim().ToUpperInvariant() ?? IpdConstants.EwsConsciousnessLevel.Alert;
                if (!IpdConstants.EwsConsciousnessLevel.All.Contains(consciousness))
                    return new RecordEarlyWarningScoreResponseModel { Success = false, Message = "Invalid consciousness level." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordEarlyWarningScoreResponseModel { Success = false, Message = "Admission not found." };

                var rrScore = EarlyWarningScoreCalculator.ComputeRespiratoryScore(request.RespiratoryRate);
                var spo2Score = EarlyWarningScoreCalculator.ComputeSpo2Score(request.Spo2);
                var o2Score = EarlyWarningScoreCalculator.ComputeOxygenScore(request.SupplementalOxygen);
                var bpScore = EarlyWarningScoreCalculator.ComputeBloodPressureScore(request.SystolicBp);
                var pulseScore = EarlyWarningScoreCalculator.ComputePulseScore(request.Pulse);
                var consciousnessScore = EarlyWarningScoreCalculator.ComputeConsciousnessScore(consciousness);
                var tempScore = EarlyWarningScoreCalculator.ComputeTemperatureScore(request.TemperatureC);
                var totalScore = rrScore + spo2Score + o2Score + bpScore + pulseScore + consciousnessScore + tempScore;
                var anyComponentIsThree = new[] { rrScore, spo2Score, bpScore, pulseScore, consciousnessScore, tempScore }.Any(s => s == 3);
                var riskBand = EarlyWarningScoreCalculator.ComputeRiskBand(totalScore, anyComponentIsThree);

                var now = DateTime.UtcNow;
                var score = new EarlyWarningScore
                {
                    ScoreId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    RespiratoryRate = request.RespiratoryRate,
                    Spo2 = request.Spo2,
                    SupplementalOxygen = request.SupplementalOxygen,
                    SystolicBp = request.SystolicBp,
                    Pulse = request.Pulse,
                    ConsciousnessLevel = consciousness,
                    TemperatureC = request.TemperatureC,
                    RrScore = rrScore,
                    Spo2Score = spo2Score,
                    O2Score = o2Score,
                    BpScore = bpScore,
                    PulseScore = pulseScore,
                    ConsciousnessScore = consciousnessScore,
                    TempScore = tempScore,
                    TotalScore = totalScore,
                    RiskBand = riskBand,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    ScoredBy = request.LoggedInUserName ?? "Unknown",
                    ScoredAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.EarlyWarningScore.Add(score);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordEarlyWarningScoreResponseModel
                {
                    Success = true,
                    Message = "Early Warning Score recorded.",
                    ScoreId = score.ScoreId,
                    TotalScore = totalScore,
                    RiskBand = riskBand,
                    EscalationRecommended = riskBand == IpdConstants.EwsRiskBand.Medium || riskBand == IpdConstants.EwsRiskBand.High,
                };
            }
            catch (Exception)
            {
                return new RecordEarlyWarningScoreResponseModel { Success = false, Message = "Error recording the Early Warning Score." };
            }
        }
    }
}
