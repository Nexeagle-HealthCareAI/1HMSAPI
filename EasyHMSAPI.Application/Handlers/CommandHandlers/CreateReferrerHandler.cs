using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreateReferrerHandler : IRequestHandler<CreateReferrerRequestModel, CreateReferrerResponseModel>
    {
        private readonly AppDbContext _context;
        public CreateReferrerHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateReferrerResponseModel> Handle(CreateReferrerRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ReferrerName))
                throw new ArgumentException("Referrer name is required.");

            var rate = request.DefaultRatePercent;
            if (rate < 0 || rate > 100)
                throw new ArgumentException("DefaultRatePercent must be between 0 and 100.");

            var referrer = new Referrer
            {
                ReferrerId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                ReferrerName = request.ReferrerName.Trim(),
                ReferrerType = string.IsNullOrWhiteSpace(request.ReferrerType) ? "REFERRER" : request.ReferrerType.Trim().ToUpper(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                Pan = string.IsNullOrWhiteSpace(request.Pan) ? null : request.Pan.Trim().ToUpper(),
                DefaultRatePercent = rate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.UserId?.ToString(),
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.UserId?.ToString()
            };

            _context.Referrers.Add(referrer);
            await _context.SaveChangesAsync(cancellationToken);

            return new CreateReferrerResponseModel
            {
                ReferrerId = referrer.ReferrerId,
                Message = "Referrer created successfully"
            };
        }
    }
}
