using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Saves a doctor's personalized discharge-summary field layout, scoped to one hospital —
    /// upserts the (DoctorId, HospitalId) row, storing the ordered field list as JSON. Always
    /// writes a hospital-specific row, never the pre-migration legacy (HospitalId IS NULL) row.
    /// Mirrors UpdateDoctorPrescriptionFieldConfigHandler (still doctor-global, unlike this one).
    /// </summary>
    public class UpdateDoctorDischargeFieldConfigHandler
        : IRequestHandler<UpdateDoctorDischargeFieldConfigRequestModel, UpdateDoctorDischargeFieldConfigResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateDoctorDischargeFieldConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateDoctorDischargeFieldConfigResponseModel> Handle(
            UpdateDoctorDischargeFieldConfigRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new UpdateDoctorDischargeFieldConfigResponseModel { Success = false, Message = "Doctor is required." };
            if (request.HospitalId == Guid.Empty)
                return new UpdateDoctorDischargeFieldConfigResponseModel { Success = false, Message = "Hospital is required." };

            var configJson = JsonSerializer.Serialize(request.Fields ?? new());
            var now = DateTime.UtcNow;

            var row = await _context.DoctorDischargeFieldConfigs
                .FirstOrDefaultAsync(c => c.DoctorId == request.DoctorId && c.HospitalId == request.HospitalId, cancellationToken);

            if (row == null)
            {
                _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
                {
                    ConfigId = Guid.NewGuid(),
                    DoctorId = request.DoctorId,
                    HospitalId = request.HospitalId,
                    ConfigJson = configJson,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
            else
            {
                row.ConfigJson = configJson;
                row.UpdatedAtUtc = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return new UpdateDoctorDischargeFieldConfigResponseModel { Success = true, Message = "Field layout saved." };
        }
    }
}
