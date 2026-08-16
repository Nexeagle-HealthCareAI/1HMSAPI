using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePathologyReportTemplateCommandHandler : IRequestHandler<UpdatePathologyReportTemplateRequestModel, bool>
    {
        private readonly AppDbContext _context;

        public UpdatePathologyReportTemplateCommandHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdatePathologyReportTemplateRequestModel request, CancellationToken cancellationToken)
        {
            var template = await _context.PathologyReportTemplate
                .FirstOrDefaultAsync(x => x.TemplateId == request.TemplateId && x.HospitalId == request.HospitalId, cancellationToken);

            if (template == null)
            {
                throw new ApplicationException("Pathology report template not found.");
            }

            if (template.TemplateCode != request.TemplateCode)
            {
                if (await _context.PathologyReportTemplate.AnyAsync(x => x.HospitalId == request.HospitalId && x.TemplateCode == request.TemplateCode, cancellationToken))
                {
                    throw new ApplicationException($"Template code {request.TemplateCode} already exists for this hospital.");
                }
            }

            if (request.IsDefault && !template.IsDefault)
            {
                var existingDefaults = await _context.PathologyReportTemplate
                    .Where(x => x.HospitalId == request.HospitalId && x.IsDefault)
                    .ToListAsync(cancellationToken);
                
                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                    _context.PathologyReportTemplate.Update(existing);
                }
            }

            template.TemplateCode = request.TemplateCode;
            template.TemplateName = request.TemplateName;
            template.HeaderBlobPath = request.HeaderBlobPath;
            template.LayoutJson = request.LayoutJson;
            template.FooterText = request.FooterText;
            template.IsDefault = request.IsDefault;
            template.IsActive = request.IsActive;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = request.LoggedInUserName ?? "System";

            _context.PathologyReportTemplate.Update(template);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
