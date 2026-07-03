using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Upsert-with-versioning: a new version for the same (HospitalId, TypeCode, Language) flips
    /// the prior active row's IsActive off and inserts a new row with Version+1. TypeCode gets no
    /// hard validation — the DB has no CHECK on this column, deliberately loose.
    /// </summary>
    public class ConsentTemplateCommandHandlers : IRequestHandler<UpsertConsentTemplateRequestModel, UpsertConsentTemplateResponseModel>
    {
        private readonly AppDbContext _context;

        public ConsentTemplateCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertConsentTemplateResponseModel> Handle(UpsertConsentTemplateRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.TypeCode))
                    return new UpsertConsentTemplateResponseModel { Success = false, Message = "HospitalId and TypeCode are required." };

                var typeCode = request.TypeCode.Trim().ToUpperInvariant();
                var language = string.IsNullOrWhiteSpace(request.Language) ? "EN" : request.Language.Trim().ToUpperInvariant();

                var priorActive = await _context.ConsentTemplate
                    .Where(t => t.HospitalId == request.HospitalId && t.TypeCode == typeCode && t.Language == language && t.IsActive)
                    .OrderByDescending(t => t.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                var now = DateTime.UtcNow;
                var newVersion = (priorActive?.Version ?? 0) + 1;

                if (priorActive != null)
                {
                    priorActive.IsActive = false;
                    priorActive.UpdatedAt = now;
                    priorActive.UpdatedBy = request.LoggedInUserName;
                }

                var template = new ConsentTemplate
                {
                    ConsentTemplateId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    TypeCode = typeCode,
                    Title = string.IsNullOrWhiteSpace(request.Title) ? priorActive?.Title : request.Title.Trim(),
                    Language = language,
                    Version = newVersion,
                    BodyHtml = request.BodyHtml,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.ConsentTemplate.Add(template);

                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertConsentTemplateResponseModel
                {
                    Success = true,
                    Message = "Consent template saved.",
                    ConsentTemplateId = template.ConsentTemplateId,
                    Version = template.Version,
                };
            }
            catch (Exception)
            {
                return new UpsertConsentTemplateResponseModel { Success = false, Message = "Error saving consent template." };
            }
        }
    }
}
