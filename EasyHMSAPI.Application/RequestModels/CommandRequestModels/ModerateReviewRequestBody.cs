using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Thin request body for DoctorsController.ModerateDoctorReview — HospitalId/ReviewId come
    // from the route/query instead, so they can't be spoofed independently of the URL being called.
    [ExcludeFromCodeCoverage]
    public class ModerateReviewRequestBody
    {
        public bool IsHidden { get; set; }
    }
}
