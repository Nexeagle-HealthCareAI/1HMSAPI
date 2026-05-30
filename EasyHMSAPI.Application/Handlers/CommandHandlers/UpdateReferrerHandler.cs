using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateReferrerHandler : IRequestHandler<UpdateReferrerRequestModel, UpdateReferrerResponseModel>
    {
        private readonly AppDbContext _context;
        public UpdateReferrerHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateReferrerResponseModel> Handle(UpdateReferrerRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ReferrerName))
                throw new ArgumentException("Referrer name is required.");
            if (request.DefaultRatePercent < 0 || request.DefaultRatePercent > 100)
                throw new ArgumentException("DefaultRatePercent must be between 0 and 100.");

            var referrer = await _context.Referrers
                .FirstOrDefaultAsync(r => r.ReferrerId == request.ReferrerId && r.HospitalId == request.HospitalId, cancellationToken);

            if (referrer == null)
                return new UpdateReferrerResponseModel { Success = false, ReferrerId = request.ReferrerId, Message = "Referrer not found." };

            referrer.ReferrerName = request.ReferrerName.Trim();
            referrer.ReferrerType = string.IsNullOrWhiteSpace(request.ReferrerType) ? "REFERRER" : request.ReferrerType.Trim().ToUpper();
            referrer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            referrer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            referrer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            referrer.Pan = string.IsNullOrWhiteSpace(request.Pan) ? null : request.Pan.Trim().ToUpper();
            referrer.DefaultRatePercent = request.DefaultRatePercent;
            if (request.IsActive.HasValue)
                referrer.IsActive = request.IsActive.Value;
            referrer.UpdatedAt = DateTime.UtcNow;
            referrer.UpdatedBy = request.UserId?.ToString();

            await _context.SaveChangesAsync(cancellationToken);

            return new UpdateReferrerResponseModel { Success = true, ReferrerId = referrer.ReferrerId, Message = "Referrer updated successfully" };
        }
    }
}
