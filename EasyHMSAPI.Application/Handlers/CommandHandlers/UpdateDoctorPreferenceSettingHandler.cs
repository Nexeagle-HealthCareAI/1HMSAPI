using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateDoctorPreferenceSettingHandler : IRequestHandler<UpdateDoctorPreferenceSettingRequestModel, UpdateDoctorPreferenceSettingResponseModel>
    {
        private readonly AppDbContext _context;
        public UpdateDoctorPreferenceSettingHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateDoctorPreferenceSettingResponseModel> Handle(UpdateDoctorPreferenceSettingRequestModel request, CancellationToken cancellationToken)
        {
            var preference = await _context.DoctorSectionPreferences.FirstOrDefaultAsync(p => p.DoctorId == request.DoctorId && p.HospitalId == request.HospitalId, cancellationToken);
            if (preference == null)
            {
                return new UpdateDoctorPreferenceSettingResponseModel
                {
                    Success = false,
                    Message = "Doctor preference setting not found."
                };
            }

            var update = request.Preference;
            if (update != null)
            {
                if (update.Vitals.HasValue) preference.Vitals = update.Vitals.Value;
                if (update.ChiefComplaint.HasValue) preference.ChiefComplaint = update.ChiefComplaint.Value;
                if (update.History.HasValue) preference.History = update.History.Value;
                if (update.Comorbidity.HasValue) preference.Comorbidity = update.Comorbidity.Value;
                if (update.Examination.HasValue) preference.Examination = update.Examination.Value;
                if (update.Diagnosis.HasValue) preference.Diagnosis = update.Diagnosis.Value;
                if (update.Investigations.HasValue) preference.Investigations = update.Investigations.Value;
                if (update.Procedures.HasValue) preference.Procedures = update.Procedures.Value;
                if (update.Medications.HasValue) preference.Medications = update.Medications.Value;
                if (update.PrivateNotes.HasValue) preference.PrivateNotes = update.PrivateNotes.Value;
                if (update.CertificatesAndNotes.HasValue) preference.CertificatesAndNotes = update.CertificatesAndNotes.Value;
                if (update.Immunizations.HasValue) preference.Immunizations = update.Immunizations.Value;
                if (update.FollowUpAndReferral.HasValue) preference.FollowUpAndReferral = update.FollowUpAndReferral.Value;
                if (update.NonPharmacologicalAdvice.HasValue) preference.NonPharmacologicalAdvice = update.NonPharmacologicalAdvice.Value;
                if (update.Attachments.HasValue) preference.Attachments = update.Attachments.Value;
            }
            preference.UpdatedAtUtc = System.DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return new UpdateDoctorPreferenceSettingResponseModel
            {
                Success = true,
                Message = "Doctor preference setting updated successfully."
            };
        }
    }
}