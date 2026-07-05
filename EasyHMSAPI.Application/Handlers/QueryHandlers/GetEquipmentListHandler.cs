using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetEquipmentListHandler : IRequestHandler<GetEquipmentListRequestModel, GetEquipmentListResponseModel>
    {
        private readonly AppDbContext _context;

        public GetEquipmentListHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetEquipmentListResponseModel> Handle(GetEquipmentListRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Equipment.Where(e => e.HospitalId == request.HospitalId);

            if (!string.IsNullOrWhiteSpace(request.Status))
                query = query.Where(e => e.Status == request.Status.Trim().ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(request.Department))
                query = query.Where(e => e.Department == request.Department);

            if (!string.IsNullOrWhiteSpace(request.Category))
                query = query.Where(e => e.Category == request.Category.Trim().ToUpperInvariant());

            if (request.DueOnly)
            {
                var today = DateTime.UtcNow.Date;
                query = query.Where(e => e.NextDueAt != null && e.NextDueAt <= today);
            }

            var equipment = await query
                .OrderBy(e => e.NextDueAt ?? DateTime.MaxValue)
                .Select(e => new EquipmentDataModel
                {
                    EquipmentId = e.EquipmentId,
                    AssetCode = e.AssetCode,
                    Name = e.Name,
                    Model = e.Model,
                    SerialNumber = e.SerialNumber,
                    Manufacturer = e.Manufacturer,
                    Category = e.Category,
                    Location = e.Location,
                    Department = e.Department,
                    AmcVendor = e.AmcVendor,
                    InstalledAt = e.InstalledAt,
                    WarrantyEndAt = e.WarrantyEndAt,
                    AmcEndAt = e.AmcEndAt,
                    PmIntervalDays = e.PmIntervalDays,
                    LastServiceAt = e.LastServiceAt,
                    NextDueAt = e.NextDueAt,
                    Status = e.Status,
                    Notes = e.Notes,
                })
                .ToListAsync(cancellationToken);

            return new GetEquipmentListResponseModel { Equipment = equipment };
        }
    }
}
