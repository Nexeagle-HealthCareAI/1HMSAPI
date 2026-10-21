using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Deletes everything the patient account owns: health-locker files and rows, family members, the short profile
    /// (PatientAccountProfiles), the app profile and app devices; then revokes every session.
    ///
    /// Kept on purpose: appointments, registrations and documents a hospital attached to a visit — those are the
    /// hospital's clinical records, not the account's. The PublicPatientAuths row is kept too (OTP cleared,
    /// SessionEpoch bumped): deleting it would reset SessionEpoch to 0 on the next sign-up and old tokens would
    /// validate again.
    ///
    /// Idempotent: if any file fails to delete, the rows for files that did go are removed, nothing else is touched,
    /// and the caller is told to retry; a second call finishes the job.
    /// </summary>
    public class DeletePatientAccountHandler : IRequestHandler<DeletePatientAccountRequestModel, DeletePatientAccountResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public DeletePatientAccountHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:HealthLockerContainer"] ?? "healthlocker";
        }

        public async Task<DeletePatientAccountResponseModel> Handle(DeletePatientAccountRequestModel request, CancellationToken ct)
        {
            var mobile = request.Mobile;
            if (string.IsNullOrWhiteSpace(mobile))
                return new DeletePatientAccountResponseModel { StatusCode = 401, Message = "Please log in again." };

            var response = new DeletePatientAccountResponseModel();

            // 1. Health-locker files first: a row is only removed once its file is gone.
            var documents = await _context.PatientHealthLockerDocuments.Where(d => d.Mobile == mobile).ToListAsync(ct);
            foreach (var document in documents)
            {
                bool deleted;
                try
                {
                    deleted = await _blobStorageService.DeleteAsync(document.DocumentId.ToString(), _containerName, ct);
                }
                catch
                {
                    deleted = false;
                }

                if (deleted)
                {
                    _context.PatientHealthLockerDocuments.Remove(document);
                    response.DocumentsDeleted++;
                }
                else
                {
                    response.DocumentsPending++;
                }
            }

            if (response.DocumentsPending > 0)
            {
                await _context.SaveChangesAsync(ct);
                response.StatusCode = 502;
                response.Message = "Some of your files could not be removed right now. Please try again.";
                return response;
            }

            // 2. Everything else the account owns.
            var members = await _context.PatientFamilyMembers.Where(m => m.OwnerMobile == mobile).ToListAsync(ct);
            _context.PatientFamilyMembers.RemoveRange(members);
            response.FamilyMembersDeleted = members.Count;

            var profile = await _context.PatientAccountProfiles.FirstOrDefaultAsync(p => p.Mobile == mobile, ct);
            if (profile != null) _context.PatientAccountProfiles.Remove(profile);

            var appProfile = await _context.PatientAppProfiles.FirstOrDefaultAsync(p => p.Mobile == mobile, ct);
            if (appProfile != null) _context.PatientAppProfiles.Remove(appProfile);

            var devices = await _context.PatientAppDevices.Where(d => d.Mobile == mobile).ToListAsync(ct);
            _context.PatientAppDevices.RemoveRange(devices);

            // 3. Sign out everywhere.
            var auth = await _context.PublicPatientAuths.FirstOrDefaultAsync(a => a.Mobile == mobile, ct);
            if (auth != null)
            {
                auth.SessionEpoch += 1;
                auth.Otp = null;
                auth.OtpSentAt = null;
                auth.OtpExpireAt = null;
                auth.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);

            response.Success = true;
            response.Message = "Your account has been deleted. Visit records held by hospitals are kept by those hospitals.";
            return response;
        }
    }
}
