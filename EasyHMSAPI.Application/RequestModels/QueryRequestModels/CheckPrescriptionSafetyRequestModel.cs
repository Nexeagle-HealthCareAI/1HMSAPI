using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CheckPrescriptionSafetyRequestModel : IRequest<CheckPrescriptionSafetyResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
        public List<SafetyCheckMedicine> Medicines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SafetyCheckMedicine
    {
        /// <summary>Brand / display name as typed or picked.</summary>
        public string? Name { get; set; }
        /// <summary>Generic / composition when known (matching works on either).</summary>
        public string? GenericName { get; set; }
    }
}
