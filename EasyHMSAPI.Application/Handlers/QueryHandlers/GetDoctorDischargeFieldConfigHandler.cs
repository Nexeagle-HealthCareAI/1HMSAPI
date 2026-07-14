using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Returns a doctor's personalized discharge-summary field layout, scoped to one hospital. If
    /// no hospital-specific row exists yet, falls back to the doctor's pre-migration legacy row
    /// (HospitalId IS NULL) so nobody's existing customization silently disappears the first time
    /// they load a hospital after this became hospital-scoped. If neither exists, returns an empty
    /// list and the client falls back to its built-in defaults. Mirrors
    /// GetDoctorPrescriptionFieldConfigHandler (still doctor-global, unlike this one).
    /// </summary>
    public class GetDoctorDischargeFieldConfigHandler
        : IRequestHandler<GetDoctorDischargeFieldConfigRequestModel, GetDoctorDischargeFieldConfigResponseModel>
    {
        private readonly AppDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public GetDoctorDischargeFieldConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDoctorDischargeFieldConfigResponseModel> Handle(
            GetDoctorDischargeFieldConfigRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new GetDoctorDischargeFieldConfigResponseModel { Success = false, Message = "Doctor is required." };

            var row = await _context.DoctorDischargeFieldConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.DoctorId == request.DoctorId && c.HospitalId == request.HospitalId, cancellationToken)
                ?? await _context.DoctorDischargeFieldConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.DoctorId == request.DoctorId && c.HospitalId == null, cancellationToken);

            var fields = new List<DischargeFieldConfigItemModel>();
            if (row != null && !string.IsNullOrWhiteSpace(row.ConfigJson))
            {
                try
                {
                    fields = JsonSerializer.Deserialize<List<DischargeFieldConfigItemModel>>(row.ConfigJson, JsonOptions)
                             ?? new List<DischargeFieldConfigItemModel>();
                }
                catch
                {
                    fields = new List<DischargeFieldConfigItemModel>();
                }
            }

            return new GetDoctorDischargeFieldConfigResponseModel
            {
                Success = true,
                Message = fields.Count > 0 ? "Field layout retrieved." : "No saved layout; using defaults.",
                Fields = fields
            };
        }
    }
}
