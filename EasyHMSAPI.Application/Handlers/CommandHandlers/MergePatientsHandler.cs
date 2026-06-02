using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Merge a duplicate patient into a canonical one. Repoints the UHID (PatientId) on every
    /// linked table atomically, backfills missing canonical demographics, and retires the duplicate
    /// registration (MergedIntoPatientId) so it disappears from pickers but old UHIDs still resolve.
    /// Irreversible by design — guarded by an explicit confirm in the UI. [[admission-module]]
    /// </summary>
    public class MergePatientsHandler : IRequestHandler<MergePatientsRequestModel, MergePatientsResponseModel>
    {
        private readonly AppDbContext _context;

        public MergePatientsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MergePatientsResponseModel> Handle(MergePatientsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.CanonicalPatientId)
                || string.IsNullOrWhiteSpace(request.DuplicatePatientId))
                return new MergePatientsResponseModel { Success = false, Message = "HospitalId, canonical and duplicate patient ids are required." };

            var canonicalId = request.CanonicalPatientId.Trim();
            var duplicateId = request.DuplicatePatientId.Trim();

            if (string.Equals(canonicalId, duplicateId, StringComparison.OrdinalIgnoreCase))
                return new MergePatientsResponseModel { Success = false, Message = "Cannot merge a patient into itself." };

            var canonical = await _context.PatientRegistrations
                .FirstOrDefaultAsync(p => p.PatientId == canonicalId && p.HospitalId == request.HospitalId, cancellationToken);
            var duplicate = await _context.PatientRegistrations
                .FirstOrDefaultAsync(p => p.PatientId == duplicateId && p.HospitalId == request.HospitalId, cancellationToken);

            if (canonical == null) return new MergePatientsResponseModel { Success = false, Message = "Canonical patient not found." };
            if (duplicate == null) return new MergePatientsResponseModel { Success = false, Message = "Duplicate patient not found." };
            if (!string.IsNullOrWhiteSpace(canonical.MergedIntoPatientId))
                return new MergePatientsResponseModel { Success = false, Message = $"The canonical patient was itself merged into {canonical.MergedIntoPatientId}. Use that UHID instead." };
            if (!string.IsNullOrWhiteSpace(duplicate.MergedIntoPatientId))
                return new MergePatientsResponseModel { Success = false, Message = "The duplicate patient is already merged." };

            var now = DateTime.UtcNow;
            var moved = new Dictionary<string, int>();

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Repoint UHID on every linked table (set-based, runs inside the transaction).
                moved["Admission"] = await _context.Admission.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["AdmissionDayBill"] = await _context.AdmissionDayBill.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["Appointment"] = await _context.Appointments.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["AppointmentVitals"] = await _context.AppointmentVitals.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["Prescription"] = await _context.Prescription.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["PrescriptionAttachment"] = await _context.PrescriptionAttachments.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["Encounter"] = await _context.Encounter.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["BillingInvoice"] = await _context.BillingInvoice.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["BillingPayment"] = await _context.BillingPayment.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["BillingChargeEvent"] = await _context.BillingChargeEvent.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["DiscountApproval"] = await _context.DiscountApproval.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["ConsentRecord"] = await _context.ConsentRecord.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);
                moved["Alert"] = await _context.Alert.Where(x => x.PatientId == duplicateId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PatientId, canonicalId), cancellationToken);

                // Backfill canonical demographics from the duplicate where the canonical value is blank.
                BackfillDemographics(canonical, duplicate);

                // Retire the duplicate registration (kept for audit / UHID resolution).
                duplicate.MergedIntoPatientId = canonicalId;
                duplicate.MergedAt = now;
                duplicate.MergedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await tx.RollbackAsync(cancellationToken);
                return new MergePatientsResponseModel { Success = false, Message = "Merge failed and was rolled back. No changes were made." };
            }

            return new MergePatientsResponseModel
            {
                Success = true,
                Message = $"Merged {duplicateId} into {canonicalId}.",
                CanonicalPatientId = canonicalId,
                MovedCounts = moved,
                TotalMoved = moved.Values.Sum(),
            };
        }

        private static void BackfillDemographics(PatientRegistration c, PatientRegistration d)
        {
            c.FullName = Pick(c.FullName, d.FullName);
            c.Mobile = Pick(c.Mobile, d.Mobile);
            c.AgeYears ??= d.AgeYears;
            c.DateOfBirth ??= d.DateOfBirth;
            c.Sex = Pick(c.Sex, d.Sex);
            c.BloodGroup = Pick(c.BloodGroup, d.BloodGroup);
            c.Religion = Pick(c.Religion, d.Religion);
            c.Nationality = Pick(c.Nationality, d.Nationality);
            c.AddressLine = Pick(c.AddressLine, d.AddressLine);
            c.FlatHouse = Pick(c.FlatHouse, d.FlatHouse);
            c.Street = Pick(c.Street, d.Street);
            c.Block = Pick(c.Block, d.Block);
            c.City = Pick(c.City, d.City);
            c.District = Pick(c.District, d.District);
            c.State = Pick(c.State, d.State);
            c.Pincode = Pick(c.Pincode, d.Pincode);
            c.Country = Pick(c.Country, d.Country);
            c.AlternateMobile = Pick(c.AlternateMobile, d.AlternateMobile);
            c.Email = Pick(c.Email, d.Email);
            c.EmergencyContactName = Pick(c.EmergencyContactName, d.EmergencyContactName);
            c.EmergencyContactRelation = Pick(c.EmergencyContactRelation, d.EmergencyContactRelation);
            c.EmergencyContactPhone = Pick(c.EmergencyContactPhone, d.EmergencyContactPhone);
            c.AadhaarNumber = Pick(c.AadhaarNumber, d.AadhaarNumber);
            c.PanNumber = Pick(c.PanNumber, d.PanNumber);
            c.AbhaId = Pick(c.AbhaId, d.AbhaId);
            c.InsuranceId = Pick(c.InsuranceId, d.InsuranceId);
        }

        private static string? Pick(string? canonical, string? duplicate)
            => string.IsNullOrWhiteSpace(canonical) ? duplicate : canonical;
    }
}
