using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Platform-wide — returns doctors across every publicly-listed hospital, not one
    // hospital scoped by an API key. City/State/SpecialtyCategory/Search are all
    // optional narrowing filters, pushed into the SQL query itself rather than
    // filtered in-memory after the fact — needed now that the platform-wide doctor
    // count has grown past what "return everyone, every time" can serve efficiently.
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorsRequestModel : IRequest<GetPublicDoctorsResponseModel>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public string? City { get; set; }
        public string? State { get; set; }
        // Matches dbo.MedicalSpecialities.PatientFacingCategory verbatim (e.g. "Neurologist"),
        // not the NexEagleWebsite specialtyId slug — the caller (Next.js proxy route) is
        // responsible for that translation, same split of responsibility already used
        // between NexEagleWebsite's specialtyId taxonomy and this DB's NMC categories.
        public string? SpecialtyCategory { get; set; }
        public string? Search { get; set; }
    }
}
