using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetNursingAssessmentsHandler : IRequestHandler<GetNursingAssessmentsRequestModel, GetNursingAssessmentsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetNursingAssessmentsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetNursingAssessmentsResponseModel> Handle(GetNursingAssessmentsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetNursingAssessmentsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var assessments = await _context.NursingAssessment
                    .Where(n => n.HospitalId == request.HospitalId && n.AdmissionId == request.AdmissionId)
                    .OrderByDescending(n => n.AssessedAt)
                    .Select(n => new NursingAssessmentItem
                    {
                        NursingAssessmentId = n.NursingAssessmentId,
                        AssessedAt = n.AssessedAt,
                        AssessedBy = n.AssessedBy,
                        MorseHistoryOfFalling = n.MorseHistoryOfFalling,
                        MorseSecondaryDiagnosis = n.MorseSecondaryDiagnosis,
                        MorseAmbulatoryAid = n.MorseAmbulatoryAid,
                        MorseIvHeparinLock = n.MorseIvHeparinLock,
                        MorseGait = n.MorseGait,
                        MorseMentalStatus = n.MorseMentalStatus,
                        MorseTotal = n.MorseTotal,
                        MorseRisk = n.MorseRisk,
                        BradenSensoryPerception = n.BradenSensoryPerception,
                        BradenMoisture = n.BradenMoisture,
                        BradenActivity = n.BradenActivity,
                        BradenMobility = n.BradenMobility,
                        BradenNutrition = n.BradenNutrition,
                        BradenFrictionShear = n.BradenFrictionShear,
                        BradenTotal = n.BradenTotal,
                        BradenRisk = n.BradenRisk,
                        MustBmiScore = n.MustBmiScore,
                        MustWeightLossScore = n.MustWeightLossScore,
                        MustAcuteDiseaseScore = n.MustAcuteDiseaseScore,
                        MustTotal = n.MustTotal,
                        MustRisk = n.MustRisk,
                        Notes = n.Notes,
                    })
                    .ToListAsync(cancellationToken);

                return new GetNursingAssessmentsResponseModel { Success = true, Assessments = assessments };
            }
            catch (Exception)
            {
                return new GetNursingAssessmentsResponseModel { Success = false, Message = "Error loading nursing assessments." };
            }
        }
    }
}
