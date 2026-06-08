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
    /// Saves a doctor's personalized prescription field layout (global per doctor) — upserts the
    /// single row, storing the ordered field list as JSON.
    /// </summary>
    public class UpdateDoctorPrescriptionFieldConfigHandler
        : IRequestHandler<UpdateDoctorPrescriptionFieldConfigRequestModel, UpdateDoctorPrescriptionFieldConfigResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateDoctorPrescriptionFieldConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateDoctorPrescriptionFieldConfigResponseModel> Handle(
            UpdateDoctorPrescriptionFieldConfigRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new UpdateDoctorPrescriptionFieldConfigResponseModel { Success = false, Message = "Doctor is required." };

            var configJson = JsonSerializer.Serialize(request.Fields ?? new());
            var now = DateTime.UtcNow;

            var row = await _context.DoctorPrescriptionFieldConfigs
                .FirstOrDefaultAsync(c => c.DoctorId == request.DoctorId, cancellationToken);

            if (row == null)
            {
                _context.DoctorPrescriptionFieldConfigs.Add(new DoctorPrescriptionFieldConfig
                {
                    ConfigId = Guid.NewGuid(),
                    DoctorId = request.DoctorId,
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
            return new UpdateDoctorPrescriptionFieldConfigResponseModel { Success = true, Message = "Field layout saved." };
        }
    }
}
