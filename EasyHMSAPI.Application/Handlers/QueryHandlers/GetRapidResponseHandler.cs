using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetRapidResponseHandler :
        IRequestHandler<GetRapidResponseHistoryRequestModel, GetRapidResponseHistoryResponseModel>,
        IRequestHandler<GetOpenRapidResponsesRequestModel, GetOpenRapidResponsesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetRapidResponseHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRapidResponseHistoryResponseModel> Handle(GetRapidResponseHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var activations = await _context.RapidResponseActivation
                .Where(r => r.HospitalId == request.HospitalId && r.AdmissionId == request.AdmissionId)
                .OrderByDescending(r => r.CalledAt)
                .ToListAsync(cancellationToken);

            return new GetRapidResponseHistoryResponseModel { Activations = activations.Select(ToDataModel).ToList() };
        }

        public async Task<GetOpenRapidResponsesResponseModel> Handle(GetOpenRapidResponsesRequestModel request, CancellationToken cancellationToken)
        {
            var activations = await _context.RapidResponseActivation
                .Where(r => r.HospitalId == request.HospitalId && r.ResolvedAt == null)
                .OrderByDescending(r => r.CalledAt)
                .ToListAsync(cancellationToken);

            var patientIds = activations.Where(a => a.PatientId != null).Select(a => a.PatientId!).Distinct().ToList();
            var patients = await _context.PatientRegistrations
                .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

            return new GetOpenRapidResponsesResponseModel
            {
                Activations = activations.Select(a =>
                {
                    var model = ToDataModel(a);
                    if (a.PatientId != null && patients.TryGetValue(a.PatientId, out var patient))
                        model.PatientName = patient.FullName;
                    return model;
                }).ToList(),
            };
        }

        private static RapidResponseDataModel ToDataModel(RapidResponseActivation a) => new()
        {
            ActivationId = a.ActivationId,
            AdmissionId = a.AdmissionId,
            TriggerReason = a.TriggerReason,
            TriggeredEwsScore = a.TriggeredEwsScore,
            CalledBy = a.CalledBy,
            CalledAt = a.CalledAt,
            RespondingTeam = a.RespondingTeam,
            ArrivedAt = a.ArrivedAt,
            ResponseTimeSeconds = a.ArrivedAt.HasValue ? (int)(a.ArrivedAt.Value - a.CalledAt).TotalSeconds : null,
            Outcome = a.Outcome,
            OutcomeNotes = a.OutcomeNotes,
            ResolvedAt = a.ResolvedAt,
        };
    }
}
