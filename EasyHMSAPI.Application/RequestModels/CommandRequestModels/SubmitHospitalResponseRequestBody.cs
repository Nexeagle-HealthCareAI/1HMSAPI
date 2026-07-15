using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Thin request body for DoctorsController.SubmitHospitalResponse — HospitalId/DoctorId come
    // from the route/query instead, so they can't be spoofed independently of the URL being called.
    [ExcludeFromCodeCoverage]
    public class SubmitHospitalResponseRequestBody
    {
        public string Comment { get; set; } = null!;
    }
}
