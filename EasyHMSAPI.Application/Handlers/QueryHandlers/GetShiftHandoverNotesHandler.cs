using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetShiftHandoverNotesHandler : IRequestHandler<GetShiftHandoverNotesRequestModel, GetShiftHandoverNotesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetShiftHandoverNotesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetShiftHandoverNotesResponseModel> Handle(GetShiftHandoverNotesRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetShiftHandoverNotesResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var notes = await _context.ShiftHandoverNote
                    .Where(s => s.HospitalId == request.HospitalId && s.AdmissionId == request.AdmissionId)
                    .OrderByDescending(s => s.HandoverAt)
                    .Select(s => new ShiftHandoverNoteItem
                    {
                        ShiftHandoverNoteId = s.ShiftHandoverNoteId,
                        ShiftCode = s.ShiftCode,
                        ShiftDate = s.ShiftDate,
                        OutgoingNurseName = s.OutgoingNurseName,
                        IncomingNurseName = s.IncomingNurseName,
                        IncomingAckAt = s.IncomingAckAt,
                        IsFreeText = s.IsFreeText,
                        FreeTextNote = s.FreeTextNote,
                        Situation = s.Situation,
                        Background = s.Background,
                        Assessment = s.Assessment,
                        Recommendation = s.Recommendation,
                        HandoverAt = s.HandoverAt,
                    })
                    .ToListAsync(cancellationToken);

                return new GetShiftHandoverNotesResponseModel { Success = true, Notes = notes };
            }
            catch (Exception)
            {
                return new GetShiftHandoverNotesResponseModel { Success = false, Message = "Error loading shift handover notes." };
            }
        }
    }
}
