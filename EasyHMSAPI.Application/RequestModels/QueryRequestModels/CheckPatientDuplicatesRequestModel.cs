using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>
    /// Probe for probable/possible/near-certain duplicate patients before a new UHID is created.
    /// Advisory only — the caller decides whether to reuse an existing patient or proceed.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CheckPatientDuplicatesRequestModel : IRequest<CheckPatientDuplicatesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AadhaarNumber { get; set; }
        // Exclude a known patient (e.g. when editing an already-selected returning patient).
        public string? ExcludePatientId { get; set; }
    }
}
