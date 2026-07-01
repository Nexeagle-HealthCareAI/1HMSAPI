using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Signs one consent record. Snapshots the template's type/title/language/version/body at
    /// signing time so later template edits never retroactively change what was signed. Pure
    /// insert — no update handler exists or is needed (ConsentRecord is immutable by design).
    /// </summary>
    public class ConsentRecordCommandHandlers : IRequestHandler<SignConsentRequestModel, SignConsentResponseModel>
    {
        private readonly AppDbContext _context;

        public ConsentRecordCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SignConsentResponseModel> Handle(SignConsentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.ConsentTemplateId == Guid.Empty)
                    return new SignConsentResponseModel { Success = false, Message = "HospitalId, AdmissionId and ConsentTemplateId are required." };

                if (string.IsNullOrWhiteSpace(request.SignedByName) || string.IsNullOrWhiteSpace(request.SignerRelation))
                    return new SignConsentResponseModel { Success = false, Message = "Signed-by name and relation are required." };

                var template = await _context.ConsentTemplate
                    .FirstOrDefaultAsync(t => t.ConsentTemplateId == request.ConsentTemplateId && t.HospitalId == request.HospitalId, cancellationToken);
                if (template == null)
                    return new SignConsentResponseModel { Success = false, Message = "Consent template not found." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new SignConsentResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new SignConsentResponseModel { Success = false, Message = "Admission is not active." };

                var now = DateTime.UtcNow;
                var record = new ConsentRecord
                {
                    ConsentRecordId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    ConsentTemplateId = template.ConsentTemplateId,
                    TemplateTypeCode = template.TypeCode,
                    TemplateTitle = template.Title,
                    TemplateLanguage = template.Language,
                    TemplateVersion = template.Version,
                    TemplateBodyHtmlSnapshot = template.BodyHtml,
                    ProcedureName = string.IsNullOrWhiteSpace(request.ProcedureName) ? null : request.ProcedureName.Trim(),
                    SignedByName = request.SignedByName.Trim(),
                    SignerRelation = request.SignerRelation.Trim(),
                    SignerIdType = string.IsNullOrWhiteSpace(request.SignerIdType) ? null : request.SignerIdType.Trim(),
                    SignerIdNumber = string.IsNullOrWhiteSpace(request.SignerIdNumber) ? null : request.SignerIdNumber.Trim(),
                    SignatureImageBase64 = request.SignatureImageBase64,
                    WitnessName = string.IsNullOrWhiteSpace(request.WitnessName) ? null : request.WitnessName.Trim(),
                    WitnessRole = string.IsNullOrWhiteSpace(request.WitnessRole) ? null : request.WitnessRole.Trim(),
                    SignedAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.ConsentRecord.Add(record);

                await _context.SaveChangesAsync(cancellationToken);

                return new SignConsentResponseModel { Success = true, Message = "Consent signed.", ConsentRecordId = record.ConsentRecordId };
            }
            catch (Exception)
            {
                return new SignConsentResponseModel { Success = false, Message = "Error signing consent." };
            }
        }
    }
}
