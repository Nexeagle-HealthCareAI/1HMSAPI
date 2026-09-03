using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Bill-scan lookup: pharmacist enters/scans the invoice number, gets back every batch-level
    // line still eligible for return (dispensed qty minus whatever's already been returned against
    // it), so the UI never has to compute that itself.
    [ExcludeFromCodeCoverage]
    public class GetReturnableInvoiceLinesRequestModel : IRequest<GetReturnableInvoiceLinesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string InvoiceNo { get; set; } = null!;
    }
}
