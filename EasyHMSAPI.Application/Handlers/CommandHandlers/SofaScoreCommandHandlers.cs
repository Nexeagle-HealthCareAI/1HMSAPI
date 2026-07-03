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
    public class SofaScoreCommandHandlers : IRequestHandler<RecordSofaScoreRequestModel, RecordSofaScoreResponseModel>
    {
        private readonly AppDbContext _context;

        public SofaScoreCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordSofaScoreResponseModel> Handle(RecordSofaScoreRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordSofaScoreResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var vasoTier = request.VasopressorTier?.Trim().ToUpperInvariant() ?? IpdConstants.SofaVasopressorTier.None;
                if (!IpdConstants.SofaVasopressorTier.All.Contains(vasoTier))
                    return new RecordSofaScoreResponseModel { Success = false, Message = "Invalid vasopressor tier." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordSofaScoreResponseModel { Success = false, Message = "Admission not found." };

                var respiratory = SofaScoreCalculator.ComputeRespiratoryScore(request.PaO2FiO2Ratio, request.OnRespiratorySupport);
                var coagulation = SofaScoreCalculator.ComputeCoagulationScore(request.PlateletsCount);
                var liver = SofaScoreCalculator.ComputeLiverScore(request.BilirubinMgDl);
                var cardiovascular = SofaScoreCalculator.ComputeCardiovascularScore(request.MapValue, vasoTier);
                var cns = SofaScoreCalculator.ComputeCnsScore(request.GcsTotal);
                var renal = SofaScoreCalculator.ComputeRenalScore(request.CreatinineMgDl, request.UrineOutputMlPerDay);
                var totalScore = respiratory + coagulation + liver + cardiovascular + cns + renal;

                var now = DateTime.UtcNow;
                var score = new SofaScore
                {
                    SofaScoreId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    PaO2FiO2Ratio = request.PaO2FiO2Ratio,
                    OnRespiratorySupport = request.OnRespiratorySupport,
                    PlateletsCount = request.PlateletsCount,
                    BilirubinMgDl = request.BilirubinMgDl,
                    MapValue = request.MapValue,
                    VasopressorTier = vasoTier,
                    GcsTotal = request.GcsTotal,
                    CreatinineMgDl = request.CreatinineMgDl,
                    UrineOutputMlPerDay = request.UrineOutputMlPerDay,
                    RespiratoryScore = respiratory,
                    CoagulationScore = coagulation,
                    LiverScore = liver,
                    CardiovascularScore = cardiovascular,
                    CnsScore = cns,
                    RenalScore = renal,
                    TotalScore = totalScore,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    ScoredBy = request.LoggedInUserName ?? "Unknown",
                    ScoredAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.SofaScore.Add(score);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordSofaScoreResponseModel { Success = true, Message = "SOFA score recorded.", SofaScoreId = score.SofaScoreId, TotalScore = totalScore };
            }
            catch (Exception)
            {
                return new RecordSofaScoreResponseModel { Success = false, Message = "Error recording SOFA score." };
            }
        }
    }
}
