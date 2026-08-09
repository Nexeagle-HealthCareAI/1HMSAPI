using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetMedicineInfoRequestModel : IRequest<GetMedicineInfoResponseModel>
    {
        public int MedicineId { get; set; }
    }
}
