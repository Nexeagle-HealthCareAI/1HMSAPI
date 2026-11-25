using System.Text.Json;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Reusable discharge summary templates. A doctor saves the summary being written under a name
    /// and applies it to another patient later. Templates belong to the hospital; every read and
    /// write is scoped by HospitalId, and a deleted template is kept (soft delete) but never listed.
    /// </summary>
    public class DischargeTemplateHandlers :
        IRequestHandler<GetDischargeTemplatesRequestModel, DischargeTemplatesResponseModel>,
        IRequestHandler<SaveDischargeTemplateRequestModel, SaveDischargeTemplateResponseModel>,
        IRequestHandler<DeleteDischargeTemplateRequestModel, SaveDischargeTemplateResponseModel>,
        IRequestHandler<MarkDischargeTemplateUsedRequestModel, SaveDischargeTemplateResponseModel>
    {
        public const int MaxNameLength = 200;
        public const int MaxContentLength = 60_000;
        public const int MaxTemplatesPerHospital = 300;

        private readonly AppDbContext _context;

        public DischargeTemplateHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DischargeTemplatesResponseModel> Handle(GetDischargeTemplatesRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new() { Success = false, Message = "HospitalId is required." };

            var rows = await _context.DischargeTemplate.AsNoTracking()
                .Where(t => t.HospitalId == request.HospitalId && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            // Most used first, then most recently changed, then by name.
            return new()
            {
                Success = true,
                Templates = rows
                    .OrderByDescending(t => t.UseCount).ThenByDescending(t => t.UpdatedAt).ThenBy(t => t.Name)
                    .Select(ToItem).ToList(),
            };
        }

        public async Task<SaveDischargeTemplateResponseModel> Handle(SaveDischargeTemplateRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return Fail("HospitalId is required.");

            var name = CleanName(request.Name);
            if (name.Length == 0) return Fail("Give the template a name.");
            if (name.Length > MaxNameLength) return Fail($"The name is too long (at most {MaxNameLength} characters).");

            var content = request.ContentJson?.Trim() ?? string.Empty;
            if (!IsJsonObject(content)) return Fail("The template has no content to save.");
            if (content.Length > MaxContentLength) return Fail("The template is too long to save.");

            var live = await _context.DischargeTemplate
                .Where(t => t.HospitalId == request.HospitalId && !t.IsDeleted)
                .ToListAsync(cancellationToken);
            var sameName = live.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            var now = DateTime.UtcNow;

            DischargeTemplate? row;
            if (request.DischargeTemplateId is { } id && id != Guid.Empty)
            {
                row = live.FirstOrDefault(t => t.DischargeTemplateId == id);
                if (row == null) return Fail("That template no longer exists.");
                if (sameName != null && sameName.DischargeTemplateId != row.DischargeTemplateId)
                    return new() { Success = false, NameTaken = true, Message = "Another template already has this name." };
            }
            else if (sameName != null)
            {
                if (!request.ReplaceExisting)
                    return new() { Success = false, NameTaken = true, Message = "A template with this name already exists." };
                row = sameName;
            }
            else
            {
                if (live.Count >= MaxTemplatesPerHospital)
                    return Fail($"This hospital already has {MaxTemplatesPerHospital} templates. Delete one you no longer use first.");
                row = new DischargeTemplate
                {
                    DischargeTemplateId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    CreatedByUserId = request.CallerUserId == Guid.Empty ? null : request.CallerUserId,
                    CreatedAt = now,
                };
                _context.DischargeTemplate.Add(row);
            }

            row.Name = name;
            row.ContentJson = content;
            row.UpdatedByUserId = request.CallerUserId == Guid.Empty ? null : request.CallerUserId;
            row.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            return new() { Success = true, Message = "Template saved.", Template = ToItem(row) };
        }

        public async Task<SaveDischargeTemplateResponseModel> Handle(DeleteDischargeTemplateRequestModel request, CancellationToken cancellationToken)
        {
            var row = await FindAsync(request.HospitalId, request.DischargeTemplateId, cancellationToken);
            if (row == null) return Fail("That template no longer exists.");

            row.IsDeleted = true;
            row.UpdatedByUserId = request.CallerUserId == Guid.Empty ? null : request.CallerUserId;
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return new() { Success = true, Message = "Template deleted." };
        }

        public async Task<SaveDischargeTemplateResponseModel> Handle(MarkDischargeTemplateUsedRequestModel request, CancellationToken cancellationToken)
        {
            var row = await FindAsync(request.HospitalId, request.DischargeTemplateId, cancellationToken);
            if (row == null) return Fail("That template no longer exists.");

            row.UseCount += 1;
            row.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return new() { Success = true, Template = ToItem(row) };
        }

        // Always by hospital as well as id, so one hospital can never touch another's template.
        private Task<DischargeTemplate?> FindAsync(Guid hospitalId, Guid templateId, CancellationToken cancellationToken) =>
            hospitalId == Guid.Empty || templateId == Guid.Empty
                ? Task.FromResult<DischargeTemplate?>(null)
                : _context.DischargeTemplate.FirstOrDefaultAsync(t => t.HospitalId == hospitalId && t.DischargeTemplateId == templateId && !t.IsDeleted, cancellationToken);

        /// <summary>Trims and collapses runs of spaces, so "Normal  delivery " and "Normal delivery" are one name.</summary>
        public static string CleanName(string? name) =>
            string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        public static bool IsJsonObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().Any();
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static DischargeTemplateItem ToItem(DischargeTemplate t) => new()
        {
            DischargeTemplateId = t.DischargeTemplateId,
            Name = t.Name,
            ContentJson = t.ContentJson,
            UseCount = t.UseCount,
            LastUsedAt = t.LastUsedAt,
            UpdatedAt = t.UpdatedAt,
        };

        private static SaveDischargeTemplateResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
