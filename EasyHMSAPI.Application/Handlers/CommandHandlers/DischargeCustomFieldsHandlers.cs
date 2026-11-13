using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GetDischargeCustomFieldsRequestModel : IRequest<DischargeCustomFieldsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }

    public class SaveDischargeCustomFieldsRequestModel : IRequest<DischargeCustomFieldsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? UpdatedBy { get; set; }
        public Dictionary<string, string> Values { get; set; } = new();
    }

    public class DischargeCustomFieldsResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public Dictionary<string, string> Values { get; set; } = new();
    }

    public class GetDischargeCustomFieldsHandler : IRequestHandler<GetDischargeCustomFieldsRequestModel, DischargeCustomFieldsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetDischargeCustomFieldsHandler(AppDbContext context) => _context = context;

        public async Task<DischargeCustomFieldsResponseModel> Handle(GetDischargeCustomFieldsRequestModel request, CancellationToken cancellationToken)
        {
            var row = await _context.Set<DischargeSummaryCustomField>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.AdmissionId == request.AdmissionId && r.HospitalId == request.HospitalId, cancellationToken);
            return new DischargeCustomFieldsResponseModel { Values = Parse(row?.FieldsJson) };
        }

        internal static Dictionary<string, string> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
            catch (JsonException) { return new(); }
        }
    }

    public class SaveDischargeCustomFieldsHandler : IRequestHandler<SaveDischargeCustomFieldsRequestModel, DischargeCustomFieldsResponseModel>
    {
        private const int MaxFields = 100;
        private const int MaxValueLength = 4000;

        private readonly AppDbContext _context;
        public SaveDischargeCustomFieldsHandler(AppDbContext context) => _context = context;

        public async Task<DischargeCustomFieldsResponseModel> Handle(SaveDischargeCustomFieldsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.Values.Count > MaxFields)
                return new DischargeCustomFieldsResponseModel { Success = false, Message = "Too many custom fields." };
            if (request.Values.Any(kv => string.IsNullOrWhiteSpace(kv.Key) || kv.Key.Length > 100 || (kv.Value?.Length ?? 0) > MaxValueLength))
                return new DischargeCustomFieldsResponseModel { Success = false, Message = "A custom field key or value is too long." };

            // The admission must belong to this hospital, and a signed summary is a legal document:
            // its custom fields are frozen along with the rest (unsign first to amend).
            var summary = await _context.Set<DischargeSummary>().AsNoTracking()
                .Where(s => s.AdmissionId == request.AdmissionId && s.HospitalId == request.HospitalId)
                .Select(s => new { s.IsSigned })
                .FirstOrDefaultAsync(cancellationToken);
            if (summary?.IsSigned == true)
                return new DischargeCustomFieldsResponseModel { Success = false, Message = "The discharge summary is signed. Unsign it to change custom fields." };

            var clean = request.Values
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value!);

            var row = await _context.Set<DischargeSummaryCustomField>()
                .FirstOrDefaultAsync(r => r.AdmissionId == request.AdmissionId && r.HospitalId == request.HospitalId, cancellationToken);
            if (row == null)
            {
                row = new DischargeSummaryCustomField { AdmissionId = request.AdmissionId, HospitalId = request.HospitalId };
                _context.Set<DischargeSummaryCustomField>().Add(row);
            }
            row.FieldsJson = JsonSerializer.Serialize(clean);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = request.UpdatedBy;
            await _context.SaveChangesAsync(cancellationToken);

            return new DischargeCustomFieldsResponseModel { Values = clean };
        }
    }
}
