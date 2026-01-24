using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertInvoiceSettingsHandler
    {
        private readonly AppDbContext _context;

        public UpsertInvoiceSettingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertInvoiceSettingsResponseModel> Handle(UpsertInvoiceSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var response = new UpsertInvoiceSettingsResponseModel();
            try
            {
                var existingSettings = await _context.InvoicePrintSettings
                    .Where(s => s.HospitalId == request.HospitalId && s.InvoicePrintId == request.InvoicePrintId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingSettings is not null)
                {
                    if (request.FeaderHeight.HasValue)
                        existingSettings.HeaderHeight = request.FeaderHeight;
                    if (request.FooterHeight.HasValue)
                        existingSettings.FooterHeight = request.FooterHeight;
                    if (request.ContentLeftMargin.HasValue)
                        existingSettings.ContentLeftMargin = request.ContentLeftMargin;
                    if (request.ContentRightMargin.HasValue)
                        existingSettings.ContentRightMargin = request.ContentRightMargin;
                    if (request.OverFlowPage.HasValue)
                        existingSettings.OverFlowPage = request.OverFlowPage;
                    if (!string.IsNullOrEmpty(request.FontFamily))
                        existingSettings.FontFamily = request.FontFamily;
                    if (request.FontSize.HasValue)
                        existingSettings.FontSize = request.FontSize;
                    if (!string.IsNullOrEmpty(request.FontWeight))
                        existingSettings.FontWeight = request.FontWeight;
                    if (!string.IsNullOrEmpty(request.TextColour))
                        existingSettings.TextColour = request.TextColour;
                    existingSettings.UpdatedAt = request.CurrentDateTime;
                    await _context.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "Invoice settings updated successfully.";
                    response.InvoicePrintId = existingSettings.InvoicePrintId;
                }
                else
                {
                    var newGuid = Guid.NewGuid();
                    var newSettings = new InvoicePrintSettings
                    {
                        HospitalId = request.HospitalId,
                        InvoicePrintId = newGuid,
                        HeaderHeight = request.FeaderHeight,
                        FooterHeight = request.FooterHeight,
                        ContentLeftMargin = request.ContentLeftMargin,
                        ContentRightMargin = request.ContentRightMargin,
                        OverFlowPage = request.OverFlowPage,
                        FontFamily = request.FontFamily,
                        FontSize = request.FontSize,
                        FontWeight = request.FontWeight,
                        TextColour = request.TextColour,
                        CreatedByUserId = request.LoggedInUserId,
                        CreatedAt = request.CurrentDateTime,
                        UpdatedAt = request.CurrentDateTime
                    };
                    _context.InvoicePrintSettings.Add(newSettings);
                    await _context.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "Invoice settings inserted successfully.";
                    response.InvoicePrintId = newGuid;
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
