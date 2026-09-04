using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPathologyExternalLabHandler : IRequestHandler<UpsertPathologyExternalLabRequestModel, UpsertPathologyExternalLabResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertPathologyExternalLabHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertPathologyExternalLabResponseModel> Handle(UpsertPathologyExternalLabRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.LabName))
                    return new UpsertPathologyExternalLabResponseModel { Success = false, Message = "HospitalId and LabName are required." };

                var now = DateTime.UtcNow;

                if (request.ExternalLabId.HasValue && request.ExternalLabId != Guid.Empty)
                {
                    var existing = await _context.PathologyExternalLab
                        .FirstOrDefaultAsync(l => l.ExternalLabId == request.ExternalLabId && l.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                        return new UpsertPathologyExternalLabResponseModel { Success = false, Message = "External lab not found." };

                    existing.LabName = request.LabName.Trim();
                    existing.ContactPerson = request.ContactPerson;
                    existing.Phone = request.Phone;
                    existing.Email = request.Email;
                    existing.Address = request.Address;
                    existing.AccreditationNo = request.AccreditationNo;
                    existing.IsActive = request.IsActive;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);
                    return new UpsertPathologyExternalLabResponseModel { Success = true, Message = "External lab updated.", ExternalLabId = existing.ExternalLabId };
                }

                var lab = new PathologyExternalLab
                {
                    ExternalLabId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    LabName = request.LabName.Trim(),
                    ContactPerson = request.ContactPerson,
                    Phone = request.Phone,
                    Email = request.Email,
                    Address = request.Address,
                    AccreditationNo = request.AccreditationNo,
                    IsActive = request.IsActive,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.PathologyExternalLab.Add(lab);
                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertPathologyExternalLabResponseModel { Success = true, Message = "External lab created.", ExternalLabId = lab.ExternalLabId };
            }
            catch (Exception)
            {
                return new UpsertPathologyExternalLabResponseModel { Success = false, Message = "Error saving external lab." };
            }
        }
    }
}
