using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAdmissionTransfusionHistoryHandler : IRequestHandler<GetAdmissionTransfusionHistoryRequestModel, GetAdmissionTransfusionHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionTransfusionHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionTransfusionHistoryResponseModel> Handle(GetAdmissionTransfusionHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var reservedBags = await _context.BloodBag
                .Where(b => b.HospitalId == request.HospitalId
                    && b.ReservedForAdmissionId == request.AdmissionId
                    && b.Status != IpdConstants.BloodBagStatus.Discarded)
                .OrderByDescending(b => b.ReservedAt)
                .Select(b => new AdmissionBloodBagDataModel
                {
                    BloodBagId = b.BloodBagId,
                    BagNumber = b.BagNumber,
                    Component = b.Component,
                    BloodGroup = b.BloodGroup,
                    Status = b.Status,
                    CrossmatchResult = b.CrossmatchResult,
                    ReservedAt = b.ReservedAt,
                })
                .ToListAsync(cancellationToken);

            var transfusions = await (
                from t in _context.TransfusionEvent
                join b in _context.BloodBag on t.BloodBagId equals b.BloodBagId
                where t.HospitalId == request.HospitalId && t.AdmissionId == request.AdmissionId
                orderby t.StartedAt descending
                select new TransfusionEventDataModel
                {
                    TransfusionEventId = t.TransfusionEventId,
                    BloodBagId = t.BloodBagId,
                    BagNumber = b.BagNumber,
                    Component = b.Component,
                    StartedAt = t.StartedAt,
                    EndedAt = t.EndedAt,
                    VolumeGivenMl = t.VolumeGivenMl,
                    Reaction = t.Reaction,
                    ReactionNotes = t.ReactionNotes,
                    AdministeredBy = t.AdministeredBy,
                    WitnessName = t.WitnessName,
                    ChargeEventId = t.ChargeEventId,
                }).ToListAsync(cancellationToken);

            return new GetAdmissionTransfusionHistoryResponseModel { ReservedBags = reservedBags, Transfusions = transfusions };
        }
    }
}
