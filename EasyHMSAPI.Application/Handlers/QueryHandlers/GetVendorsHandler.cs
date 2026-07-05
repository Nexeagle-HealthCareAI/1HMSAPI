using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetVendorsHandler : IRequestHandler<GetVendorsRequestModel, GetVendorsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetVendorsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetVendorsResponseModel> Handle(GetVendorsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Vendor.Where(v => v.HospitalId == request.HospitalId);
            if (!request.IncludeInactive)
                query = query.Where(v => v.IsActive);

            var vendors = await query
                .OrderBy(v => v.VendorName)
                .Select(v => new VendorDataModel
                {
                    VendorId = v.VendorId,
                    VendorCode = v.VendorCode,
                    VendorName = v.VendorName,
                    ContactPerson = v.ContactPerson,
                    Phone = v.Phone,
                    Email = v.Email,
                    Address = v.Address,
                    GstNumber = v.GstNumber,
                    DrugLicenseNumber = v.DrugLicenseNumber,
                    PaymentTermsDays = v.PaymentTermsDays,
                    IsActive = v.IsActive,
                })
                .ToListAsync(cancellationToken);

            return new GetVendorsResponseModel { Vendors = vendors };
        }
    }
}
