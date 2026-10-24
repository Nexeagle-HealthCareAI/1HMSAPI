using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Hospital lookup for Vita (MCP `search_hospitals`): "Sadar Hospital Purnia", "AMRI Salt Lake".
    // Only hospitals that have doctors live on Doctor Dekho (the same pool search_doctors uses) or that
    // opted into the public directory. Read-only; no patient data in the request.
    [ExcludeFromCodeCoverage]
    public class SearchPublicHospitalsRequestModel : IRequest<SearchPublicHospitalsResponseModel>
    {
        // Hospital name as the patient typed it, spelling tolerant; may include the city.
        public string? Query { get; set; }
        public string? City { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        // With Latitude/Longitude; default 25 km.
        public double? MaxDistanceKm { get; set; }
        // 1..10, default 5.
        public int Limit { get; set; } = 5;
    }
}
