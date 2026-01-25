using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetInvoiceSettingsHandler : IRequestHandler<GetInvoiceSettingsRequestModel, GetInvoiceSettingsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetInvoiceSettingsHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<GetInvoiceSettingsResponseModel> Handle(GetInvoiceSettingsRequestModel request, CancellationToken cancellationToken)
        {
            GetInvoiceSettingsResponseModel response = new();
            try
            {
                var existingHospital = await _context.Hospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if (existingHospital is null)
                {
                    response.Success = false;
                    response.Message = "Invalid hospital Id";
                }
                else
                {
                    var existingSettings = await _context.InvoicePrintSettings
                        .Where(x => x.HospitalId == request.HospitalId)
                        .Select(x => new InvoiceSettingsDataModel
                        {
                            InvoicePrintId = x.InvoicePrintId,
                            HospitalId = x.HospitalId,
                            HeaderHeight = x.HeaderHeight,
                            FooterHeight = x.FooterHeight,
                            ContentLeftMargin = x.ContentLeftMargin,
                            ContentRightMargin = x.ContentRightMargin,
                            OverFlowPage = x.OverFlowPage,
                            FontFamily = x.FontFamily,
                            FontSize = x.FontSize,
                            FontWeight = x.FontWeight,
                            TextColour = x.TextColour,
                            URI = x.URI,
                            CreatedByUserId = x.CreatedByUserId,
                            CreatedAt = x.CreatedAt,
                            UpdatedAt = x.UpdatedAt
                        })
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    response.Success = true;
                    response.Message = "Invoice settings retrieved successfully";
                    response.InvoiceSettings = existingSettings;
                }
            }
            catch(Exception ex) 
            {
                response.Success = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
