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
        // Government-verified national health ID — an exact match is deterministic proof of
        // identity (see CheckPatientDuplicatesHandler's ABHA_VERIFIED tier), unlike the other
        // heuristic signals here.
        public string? AbhaId { get; set; }
        // Exclude a known patient (e.g. when editing an already-selected returning patient).
        public string? ExcludePatientId { get; set; }
    }
}
