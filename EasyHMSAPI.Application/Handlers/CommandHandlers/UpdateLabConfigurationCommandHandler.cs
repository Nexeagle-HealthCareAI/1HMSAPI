using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateLabConfigurationCommandHandler : IRequestHandler<UpdateLabConfigurationCommand, bool>
    {
        private readonly AppDbContext _context;

        public UpdateLabConfigurationCommandHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdateLabConfigurationCommand request, CancellationToken cancellationToken)
        {
            var config = await _context.LabConfiguration
                .FirstOrDefaultAsync(x => x.HospitalId == request.HospitalId, cancellationToken);

            if (config == null)
            {
                config = new LabConfiguration
                {
                    ConfigId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AutoBillOnOrder = request.AutoBillOnOrder,
                    DefaultReportHeaderBlob = request.DefaultReportHeaderBlob,
                    DefaultReportFooterText = request.DefaultReportFooterText,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName ?? "System",
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName ?? "System"
                };
                _context.LabConfiguration.Add(config);
            }
            else
            {
                config.AutoBillOnOrder = request.AutoBillOnOrder;
                config.DefaultReportHeaderBlob = request.DefaultReportHeaderBlob;
                config.DefaultReportFooterText = request.DefaultReportFooterText;
                config.UpdatedAt = DateTime.UtcNow;
                config.UpdatedBy = request.LoggedInUserName ?? "System";
                _context.LabConfiguration.Update(config);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
