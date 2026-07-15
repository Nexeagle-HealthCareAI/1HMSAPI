using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Hide/unhide one review. Confirms the review's own HospitalId (stamped at submission time)
    // matches the caller's hospital — cheaper than re-deriving via DoctorDepartments, and the
    // review's HospitalId can never drift since it's only ever set once, at insert.
    public class ModerateDoctorReviewHandler : IRequestHandler<ModerateDoctorReviewRequestModel, ModerateDoctorReviewResponseModel>
    {
        private readonly AppDbContext _context;

        public ModerateDoctorReviewHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ModerateDoctorReviewResponseModel> Handle(ModerateDoctorReviewRequestModel request, CancellationToken cancellationToken)
        {
            var review = await _context.DoctorReviews
                .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId && r.HospitalId == request.HospitalId, cancellationToken);
            if (review == null)
                return new ModerateDoctorReviewResponseModel { Success = false, Message = "Review not found at this hospital." };

            review.IsHidden = request.IsHidden;
            await _context.SaveChangesAsync(cancellationToken);

            return new ModerateDoctorReviewResponseModel { Success = true, Message = request.IsHidden ? "Review hidden." : "Review unhidden." };
        }
    }
}
