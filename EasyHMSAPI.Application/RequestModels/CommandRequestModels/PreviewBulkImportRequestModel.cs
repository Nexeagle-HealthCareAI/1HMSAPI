using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Parses+validates a distributor spreadsheet without writing anything — the frontend shows the
    // returned grid (invalid rows highlighted) for the pharmacist to fix, then submits the corrected
    // rows to the existing POST inventory/batches/bulk endpoint to actually commit.
    [ExcludeFromCodeCoverage]
    public class PreviewBulkImportRequestModel : IRequest<PreviewBulkImportResponseModel>
    {
        public Guid HospitalId { get; set; }
        public IFormFile? File { get; set; }
    }
}
