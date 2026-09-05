using System;
using System.Collections.Generic;
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

        private static readonly HashSet<string> ValidLetterheadModes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CUSTOM_TEMPLATE", "BLANK_PREPRINTED", "SYSTEM_DEFAULT"
        };

        public async Task<bool> Handle(UpdateLabConfigurationCommand request, CancellationToken cancellationToken)
        {
            // Never let an unrecognized/missing value reach the DB -- the entity's own default
            // already covers "nothing sent," this just guards a malformed direct API call.
            var letterheadMode = ValidLetterheadModes.Contains(request.LetterheadMode ?? "")
                ? request.LetterheadMode!.ToUpperInvariant()
                : "SYSTEM_DEFAULT";

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
                    LetterheadMode = letterheadMode,
                    ReportFieldLayoutJson = request.ReportFieldLayoutJson,
                    LabName = request.LabName,
                    LabAddress = request.LabAddress,
                    LabRegistrationNumber = request.LabRegistrationNumber,
                    TechnicianName = request.TechnicianName,
                    PathologistName = request.PathologistName,
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
                config.LetterheadMode = letterheadMode;
                config.ReportFieldLayoutJson = request.ReportFieldLayoutJson;
                config.LabName = request.LabName;
                config.LabAddress = request.LabAddress;
                config.LabRegistrationNumber = request.LabRegistrationNumber;
                config.TechnicianName = request.TechnicianName;
                config.PathologistName = request.PathologistName;
                config.UpdatedAt = DateTime.UtcNow;
                config.UpdatedBy = request.LoggedInUserName ?? "System";
                _context.LabConfiguration.Update(config);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
