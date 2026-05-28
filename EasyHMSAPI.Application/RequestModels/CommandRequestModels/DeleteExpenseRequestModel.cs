using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteExpenseRequestModel : IRequest<DeleteExpenseResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ExpenseId { get; set; }
    }
}
