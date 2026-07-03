using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetRoundNotesHandler : IRequestHandler<GetRoundNotesRequestModel, GetRoundNotesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetRoundNotesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRoundNotesResponseModel> Handle(GetRoundNotesRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetRoundNotesResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var notes = await _context.RoundNote
                    .Where(r => r.HospitalId == request.HospitalId && r.AdmissionId == request.AdmissionId)
                    .OrderByDescending(r => r.NotedAt)
                    .Select(r => new RoundNoteItem
                    {
                        RoundNoteId = r.RoundNoteId,
                        DoctorId = r.DoctorId,
                        DoctorName = r.DoctorName,
                        NotedAt = r.NotedAt,
                        Subjective = r.Subjective,
                        Objective = r.Objective,
                        Assessment = r.Assessment,
                        Plan = r.Plan,
                        Diagnosis = r.Diagnosis,
                        IsAddendum = r.IsAddendum,
                        ParentNoteId = r.ParentNoteId,
                        AddendumReason = r.AddendumReason,
                    })
                    .ToListAsync(cancellationToken);

                return new GetRoundNotesResponseModel { Success = true, Notes = notes };
            }
            catch (Exception)
            {
                return new GetRoundNotesResponseModel { Success = false, Message = "Error loading round notes." };
            }
        }
    }
}
