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
    /// "Delete account" from the Doctor Dekho app: removes everything the patient keeps with Doctor Dekho — health-locker files
    /// (storage and rows), family members, their account profile, app settings and registered devices — then ends every session.
    /// Hospital-owned records (registrations, appointments, prescriptions written by a hospital) are not touched, as the app says.
    /// Safe to repeat: if a stored file can't be removed this time, everything else is still removed, the rest of that
    /// document stays for the next try, and sessions stay valid so the patient can try again.
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

        public async Task<DeletePatientAccountResponseModel> Handle(DeletePatientAccountRequestModel request, CancellationToken cancellationToken)
        {
            var mobile = request.Mobile;
            if (string.IsNullOrWhiteSpace(mobile))
                return new DeletePatientAccountResponseModel { Message = "Please log in again." };

            // Files first: a row is removed only once its file is gone, so nothing is orphaned in storage.
            var documents = await _context.PatientHealthLockerDocuments.Where(d => d.Mobile == mobile).ToListAsync(cancellationToken);
            var pending = 0;
            foreach (var doc in documents)
            {
                bool removed;
                try
                {
                    // DeleteAsync returns false when no file exists for the document: nothing left to remove, so that counts as removed.
                    await _blobStorageService.DeleteAsync(doc.DocumentId.ToString(), _containerName, cancellationToken);
                    removed = true;
                }
                catch (Exception)
                {
                    removed = false;
                }
                if (removed) _context.PatientHealthLockerDocuments.Remove(doc);
                else pending++;
            }

            _context.PatientFamilyMembers.RemoveRange(await _context.PatientFamilyMembers.Where(m => m.OwnerMobile == mobile).ToListAsync(cancellationToken));
            _context.PatientAccountProfiles.RemoveRange(await _context.PatientAccountProfiles.Where(p => p.Mobile == mobile).ToListAsync(cancellationToken));
            _context.PatientAppProfiles.RemoveRange(await _context.PatientAppProfiles.Where(p => p.Mobile == mobile).ToListAsync(cancellationToken));
            _context.PatientAppDevices.RemoveRange(await _context.PatientAppDevices.Where(d => d.Mobile == mobile).ToListAsync(cancellationToken));

            if (pending == 0)
            {
                // Every token issued so far stops working (same mechanism as log out). The sign-in row itself stays: it only holds
                // the number and the code-sending limits, which protect the number from OTP abuse.
                var auth = await _context.PublicPatientAuths.FirstOrDefaultAsync(a => a.Mobile == mobile, cancellationToken);
                if (auth != null)
                {
                    auth.SessionEpoch += 1;
                    auth.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return pending == 0
                ? new DeletePatientAccountResponseModel { Success = true, Message = "Your Doctor Dekho account and records have been deleted." }
                : new DeletePatientAccountResponseModel { Success = false, DocumentsPending = pending, Message = "Some files could not be removed yet. Please try again." };
        }
    }
}
