using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Full NMC qualification-ladder catalog (MD/MS/DM/MCh), for the doctor-profile
    // "primary speciality" picker. No filters — it's a small (~86 row), rarely-changing
    // global reference list, cheap enough to fetch whole and filter client-side.
    [ExcludeFromCodeCoverage]
    public class GetMedicalSpecialitiesRequestModel : MediatR.IRequest<GetMedicalSpecialitiesResponseModel>
    {
    }
}
