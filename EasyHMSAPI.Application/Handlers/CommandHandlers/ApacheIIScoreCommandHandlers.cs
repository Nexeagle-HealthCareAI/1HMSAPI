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
    public class ApacheIIScoreCommandHandlers : IRequestHandler<RecordApacheIIScoreRequestModel, RecordApacheIIScoreResponseModel>
    {
        private readonly AppDbContext _context;

        public ApacheIIScoreCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordApacheIIScoreResponseModel> Handle(RecordApacheIIScoreRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordApacheIIScoreResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var chronicHealth = request.ChronicHealthCategory?.Trim().ToUpperInvariant() ?? IpdConstants.ApacheChronicHealthCategory.None;
                if (!IpdConstants.ApacheChronicHealthCategory.All.Contains(chronicHealth))
                    return new RecordApacheIIScoreResponseModel { Success = false, Message = "Invalid chronic health category." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordApacheIIScoreResponseModel { Success = false, Message = "Admission not found." };

                var totalScore = ApacheIIScoreCalculator.ComputeTotal(
                    request.Temperature, request.MapValue, request.HeartRate, request.RespiratoryRate, request.PaO2,
                    request.ArterialPh, request.SerumSodium, request.SerumPotassium, request.SerumCreatinine,
                    request.IsAcuteRenalFailure, request.Hematocrit, request.Wbc, request.GcsTotal,
                    request.AgeYears, chronicHealth);

                var now = DateTime.UtcNow;
                var score = new ApacheIIScore
                {
                    ApacheIIScoreId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    Temperature = request.Temperature,
                    MapValue = request.MapValue,
                    HeartRate = request.HeartRate,
                    RespiratoryRate = request.RespiratoryRate,
                    FiO2 = request.FiO2,
                    PaO2 = request.PaO2,
                    ArterialPh = request.ArterialPh,
                    SerumSodium = request.SerumSodium,
                    SerumPotassium = request.SerumPotassium,
                    SerumCreatinine = request.SerumCreatinine,
                    IsAcuteRenalFailure = request.IsAcuteRenalFailure,
                    Hematocrit = request.Hematocrit,
                    Wbc = request.Wbc,
                    GcsTotal = request.GcsTotal,
                    AgeYears = request.AgeYears,
                    ChronicHealthCategory = chronicHealth,
                    TotalScore = totalScore,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    ScoredBy = request.LoggedInUserName ?? "Unknown",
                    ScoredAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.ApacheIIScore.Add(score);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordApacheIIScoreResponseModel { Success = true, Message = "APACHE II score recorded.", ApacheIIScoreId = score.ApacheIIScoreId, TotalScore = totalScore };
            }
            catch (Exception)
            {
                return new RecordApacheIIScoreResponseModel { Success = false, Message = "Error recording APACHE II score." };
            }
        }
    }
}
