using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VendorCommandHandlers : IRequestHandler<UpsertVendorRequestModel, UpsertVendorResponseModel>
    {
        private readonly AppDbContext _context;

        public VendorCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertVendorResponseModel> Handle(UpsertVendorRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.VendorCode) || string.IsNullOrWhiteSpace(request.VendorName))
                    return new UpsertVendorResponseModel { Success = false, Message = "HospitalId, VendorCode, and VendorName are required." };
                if (request.PaymentTermsDays < 0)
                    return new UpsertVendorResponseModel { Success = false, Message = "Payment terms cannot be negative." };

                var now = DateTime.UtcNow;

                if (request.VendorId.HasValue && request.VendorId != Guid.Empty)
                {
                    var existing = await _context.Vendor
                        .FirstOrDefaultAsync(v => v.VendorId == request.VendorId && v.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                        return new UpsertVendorResponseModel { Success = false, Message = "Vendor not found." };

                    var codeTaken = await _context.Vendor.AnyAsync(
                        v => v.HospitalId == request.HospitalId && v.VendorCode == request.VendorCode.Trim() && v.VendorId != existing.VendorId, cancellationToken);
                    if (codeTaken)
                        return new UpsertVendorResponseModel { Success = false, Message = "A vendor with this code already exists." };

                    existing.VendorCode = request.VendorCode.Trim();
                    existing.VendorName = request.VendorName.Trim();
                    existing.ContactPerson = request.ContactPerson;
                    existing.Phone = request.Phone;
                    existing.Email = request.Email;
                    existing.Address = request.Address;
                    existing.GstNumber = request.GstNumber;
                    existing.DrugLicenseNumber = request.DrugLicenseNumber;
                    existing.PaymentTermsDays = request.PaymentTermsDays;
                    existing.IsActive = request.IsActive;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);
                    return new UpsertVendorResponseModel { Success = true, Message = "Vendor updated.", VendorId = existing.VendorId };
                }

                var exists = await _context.Vendor.AnyAsync(
                    v => v.HospitalId == request.HospitalId && v.VendorCode == request.VendorCode.Trim(), cancellationToken);
                if (exists)
                    return new UpsertVendorResponseModel { Success = false, Message = "A vendor with this code already exists." };

                var vendor = new Vendor
                {
                    VendorId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    VendorCode = request.VendorCode.Trim(),
                    VendorName = request.VendorName.Trim(),
                    ContactPerson = request.ContactPerson,
                    Phone = request.Phone,
                    Email = request.Email,
                    Address = request.Address,
                    GstNumber = request.GstNumber,
                    DrugLicenseNumber = request.DrugLicenseNumber,
                    PaymentTermsDays = request.PaymentTermsDays,
                    IsActive = request.IsActive,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Vendor.Add(vendor);
                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertVendorResponseModel { Success = true, Message = "Vendor created.", VendorId = vendor.VendorId };
            }
            catch (Exception)
            {
                return new UpsertVendorResponseModel { Success = false, Message = "Error saving vendor." };
            }
        }
    }
}
