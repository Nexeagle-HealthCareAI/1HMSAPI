using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Returns a doctor's personalized prescription field layout (global per doctor). If they have no
    /// saved layout yet, returns an empty list and the client falls back to its built-in defaults.
    /// </summary>
    public class GetDoctorPrescriptionFieldConfigHandler
        : IRequestHandler<GetDoctorPrescriptionFieldConfigRequestModel, GetDoctorPrescriptionFieldConfigResponseModel>
    {
        private readonly AppDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public GetDoctorPrescriptionFieldConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDoctorPrescriptionFieldConfigResponseModel> Handle(
            GetDoctorPrescriptionFieldConfigRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new GetDoctorPrescriptionFieldConfigResponseModel { Success = false, Message = "Doctor is required." };

            var row = await _context.DoctorPrescriptionFieldConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.DoctorId == request.DoctorId, cancellationToken);

            var fields = new List<PrescriptionFieldConfigItemModel>();
            if (row != null && !string.IsNullOrWhiteSpace(row.ConfigJson))
            {
                try
                {
                    fields = JsonSerializer.Deserialize<List<PrescriptionFieldConfigItemModel>>(row.ConfigJson, JsonOptions)
                             ?? new List<PrescriptionFieldConfigItemModel>();
                }
                catch
                {
                    fields = new List<PrescriptionFieldConfigItemModel>();
                }
            }

            return new GetDoctorPrescriptionFieldConfigResponseModel
            {
                Success = true,
                Message = fields.Count > 0 ? "Field layout retrieved." : "No saved layout; using defaults.",
                Fields = fields
            };
        }
    }
}
