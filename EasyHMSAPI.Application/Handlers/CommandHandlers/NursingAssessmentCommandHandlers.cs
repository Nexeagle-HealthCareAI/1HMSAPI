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
    /// Records one nursing-assessment snapshot. Insert-only — re-assess by inserting a new row so
    /// risk trend is visible over the stay. Totals/risk bands are always server-computed.
    /// </summary>
    public class NursingAssessmentCommandHandlers : IRequestHandler<RecordNursingAssessmentRequestModel, RecordNursingAssessmentResponseModel>
    {
        private readonly AppDbContext _context;

        public NursingAssessmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordNursingAssessmentResponseModel> Handle(RecordNursingAssessmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordNursingAssessmentResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                if (!IpdConstants.MorseFallScale.HistoryOfFallingOptions.Contains(request.MorseHistoryOfFalling)
                    || !IpdConstants.MorseFallScale.SecondaryDiagnosisOptions.Contains(request.MorseSecondaryDiagnosis)
                    || !IpdConstants.MorseFallScale.AmbulatoryAidOptions.Contains(request.MorseAmbulatoryAid)
                    || !IpdConstants.MorseFallScale.IvHeparinLockOptions.Contains(request.MorseIvHeparinLock)
                    || !IpdConstants.MorseFallScale.GaitOptions.Contains(request.MorseGait)
                    || !IpdConstants.MorseFallScale.MentalStatusOptions.Contains(request.MorseMentalStatus))
                    return new RecordNursingAssessmentResponseModel { Success = false, Message = "Invalid Morse Fall Scale component value." };

                if (request.BradenSensoryPerception is < 1 or > 4 || request.BradenMoisture is < 1 or > 4
                    || request.BradenActivity is < 1 or > 4 || request.BradenMobility is < 1 or > 4
                    || request.BradenNutrition is < 1 or > 4 || request.BradenFrictionShear is < 1 or > 3)
                    return new RecordNursingAssessmentResponseModel { Success = false, Message = "Invalid Braden Scale component value." };

                if (request.MustBmiScore is < 0 or > 2 || request.MustWeightLossScore is < 0 or > 2
                    || (request.MustAcuteDiseaseScore != 0 && request.MustAcuteDiseaseScore != 2))
                    return new RecordNursingAssessmentResponseModel { Success = false, Message = "Invalid MUST component value." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordNursingAssessmentResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new RecordNursingAssessmentResponseModel { Success = false, Message = "Admission is not active." };

                var morseTotal = request.MorseHistoryOfFalling + request.MorseSecondaryDiagnosis + request.MorseAmbulatoryAid
                    + request.MorseIvHeparinLock + request.MorseGait + request.MorseMentalStatus;
                var morseRisk = IpdConstants.MorseRisk.FromTotal(morseTotal);

                var bradenTotal = request.BradenSensoryPerception + request.BradenMoisture + request.BradenActivity
                    + request.BradenMobility + request.BradenNutrition + request.BradenFrictionShear;
                var bradenRisk = IpdConstants.BradenRisk.FromTotal(bradenTotal);

                var mustTotal = request.MustBmiScore + request.MustWeightLossScore + request.MustAcuteDiseaseScore;
                var mustRisk = IpdConstants.MustRisk.FromTotal(mustTotal);

                var now = DateTime.UtcNow;
                var assessment = new NursingAssessment
                {
                    NursingAssessmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    AssessedAt = now,
                    AssessedBy = request.LoggedInUserName,
                    AssessedByUserId = request.LoggedInUserId,
                    MorseHistoryOfFalling = request.MorseHistoryOfFalling,
                    MorseSecondaryDiagnosis = request.MorseSecondaryDiagnosis,
                    MorseAmbulatoryAid = request.MorseAmbulatoryAid,
                    MorseIvHeparinLock = request.MorseIvHeparinLock,
                    MorseGait = request.MorseGait,
                    MorseMentalStatus = request.MorseMentalStatus,
                    MorseTotal = morseTotal,
                    MorseRisk = morseRisk,
                    BradenSensoryPerception = request.BradenSensoryPerception,
                    BradenMoisture = request.BradenMoisture,
                    BradenActivity = request.BradenActivity,
                    BradenMobility = request.BradenMobility,
                    BradenNutrition = request.BradenNutrition,
                    BradenFrictionShear = request.BradenFrictionShear,
                    BradenTotal = bradenTotal,
                    BradenRisk = bradenRisk,
                    MustBmiScore = request.MustBmiScore,
                    MustWeightLossScore = request.MustWeightLossScore,
                    MustAcuteDiseaseScore = request.MustAcuteDiseaseScore,
                    MustTotal = mustTotal,
                    MustRisk = mustRisk,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.NursingAssessment.Add(assessment);

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordNursingAssessmentResponseModel
                {
                    Success = true,
                    Message = "Nursing assessment recorded.",
                    NursingAssessmentId = assessment.NursingAssessmentId,
                    MorseTotal = morseTotal,
                    MorseRisk = morseRisk,
                    BradenTotal = bradenTotal,
                    BradenRisk = bradenRisk,
                    MustTotal = mustTotal,
                    MustRisk = mustRisk,
                };
            }
            catch (Exception)
            {
                return new RecordNursingAssessmentResponseModel { Success = false, Message = "Error recording nursing assessment." };
            }
        }
    }
}
