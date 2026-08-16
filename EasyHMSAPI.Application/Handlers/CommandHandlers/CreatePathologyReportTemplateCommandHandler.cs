using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreatePathologyReportTemplateCommandHandler : IRequestHandler<CreatePathologyReportTemplateRequestModel, Guid>
    {
        private readonly AppDbContext _context;

        public CreatePathologyReportTemplateCommandHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreatePathologyReportTemplateRequestModel request, CancellationToken cancellationToken)
        {
            if (await _context.PathologyReportTemplate.AnyAsync(x => x.HospitalId == request.HospitalId && x.TemplateCode == request.TemplateCode, cancellationToken))
            {
                throw new ApplicationException($"Template code {request.TemplateCode} already exists for this hospital.");
            }

            if (request.IsDefault)
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

            var template = new PathologyReportTemplate
            {
                TemplateId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                TemplateCode = request.TemplateCode,
                TemplateName = request.TemplateName,
                HeaderBlobPath = request.HeaderBlobPath,
                LayoutJson = request.LayoutJson,
                FooterText = request.FooterText,
                IsDefault = request.IsDefault,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.LoggedInUserName ?? "System",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.LoggedInUserName ?? "System"
            };

            _context.PathologyReportTemplate.Add(template);
            await _context.SaveChangesAsync(cancellationToken);

            return template.TemplateId;
        }
    }
}
