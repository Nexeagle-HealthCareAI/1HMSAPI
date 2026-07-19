using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Bumps SessionEpoch so every JWT issued before this moment stops validating (see
    // PatientTokenValidator) even though it isn't cryptographically expired yet — the closest
    // thing to real revocation a stateless JWT can have without a full token blocklist.
    public class PatientLogoutHandler : IRequestHandler<PatientLogoutRequestModel, PatientLogoutResponseModel>
    {
        private readonly AppDbContext _context;

        public PatientLogoutHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientLogoutResponseModel> Handle(PatientLogoutRequestModel request, CancellationToken cancellationToken)
        {
            var auth = await _context.PublicPatientAuths.FirstOrDefaultAsync(a => a.Mobile == request.Mobile, cancellationToken);
            if (auth != null)
            {
                auth.SessionEpoch += 1;
                auth.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return new PatientLogoutResponseModel { Success = true };
        }
    }
}
