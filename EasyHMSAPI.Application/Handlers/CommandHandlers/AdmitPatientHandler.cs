using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Admit a patient (Emergency / Elective / Day Care / LAMA). Reuses the patient master by UHID:
    /// an existing UHID refreshes the demographics, a new patient is registered with an auto UHID.
    /// Each admission gets its own auto-numbered IPD number (ADM-…); one patient can have many.
    /// Opens a payer branch (CASH/TPA/SCHEME), an IPD billing encounter (unless opted out), an
    /// optional bed assignment, and the initial status-history row — all in one transaction.
    /// </summary>
    public class AdmitPatientHandler : IRequestHandler<AdmitPatientRequestModel, AdmitPatientResponseModel>
    {
        private readonly AppDbContext _context;

        public AdmitPatientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdmitPatientResponseModel> Handle(AdmitPatientRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new AdmitPatientResponseModel { Success = false, Message = "HospitalId is required." };

                var payerType = NormalizePayerType(request.PayerType);
                if (payerType == null)
                    return new AdmitPatientResponseModel { Success = false, Message = "Invalid payer type." };

                // Offline resync idempotency: a re-sent admit with the same ClientRequestId returns
                // the admission already created for it instead of creating a duplicate.
                if (request.ClientRequestId.HasValue)
                {
                    var existing = await _context.Admission
                        .FirstOrDefaultAsync(a => a.HospitalId == request.HospitalId && a.ClientRequestId == request.ClientRequestId, cancellationToken);
                    if (existing != null)
                        return await BuildReplayResponseAsync(existing, cancellationToken);
                }

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var now = DateTime.UtcNow;

                        // ── Resolve the patient (existing UHID) or register a new one ──────────────
                        PatientRegistration? patient = null;
                        if (!string.IsNullOrWhiteSpace(request.PatientId))
                        {
                            patient = await _context.PatientRegistrations
                                .FirstOrDefaultAsync(p => p.PatientId == request.PatientId && p.HospitalId == request.HospitalId, cancellationToken);
                        }

                        var admissionType = NormalizeType(request.AdmissionType);
                        var isEmergency = admissionType == "EMERGENCY";

                        bool isNewPatient = patient == null;
                        if (isNewPatient)
                        {
                            if (string.IsNullOrWhiteSpace(request.FullName))
                            {
                                // Emergency/casualty: an unidentified patient must never be blocked at
                                // the door. Sex + approximate age are the only two things required —
                                // everything else (name, mobile, KYC) is backfilled later.
                                if (isEmergency)
                                {
                                    if (string.IsNullOrWhiteSpace(request.Sex) || (!request.Age.HasValue && !request.DateOfBirth.HasValue))
                                    {
                                        await tx.RollbackAsync(cancellationToken);
                                        return new AdmitPatientResponseModel { Success = false, Message = "For an emergency admission without a name, Sex and approximate age are required." };
                                    }
                                }
                                else
                                {
                                    await tx.RollbackAsync(cancellationToken);
                                    return new AdmitPatientResponseModel { Success = false, Message = "Patient name is required to register a new patient." };
                                }
                            }

                            patient = new PatientRegistration
                            {
                                RegistrationId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                PatientId = GenerateUhid(),
                                RegisteredAt = now,
                                Country = "India",
                            };
                            _context.PatientRegistrations.Add(patient);
                        }

                        ApplyDemographics(patient!, request);
                        if (string.IsNullOrWhiteSpace(patient!.FullName))
                            patient.FullName = SynthesizeUnknownName(request.Sex, request.Age);

                        // ── Create the admission (own IPD number) ──────────────────────────────────
                        var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                            _context, request.HospitalId, BillingConstants.NumberSeriesCode.Admission, request.LoggedInUserName, cancellationToken);
                        numberSeries.CurrentValue++;
                        var admissionNo = NumberSeriesFormatter.Format(
                            numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                        numberSeries.UpdatedAt = now;
                        numberSeries.UpdatedBy = request.LoggedInUserName;

                        // Pre-registration only makes sense for Elective (patient not yet arrived) —
                        // silently ignored for every other admission type rather than rejecting.
                        var isPreRegistration = request.IsPreRegistration && admissionType == "ELECTIVE";
                        var initialStatus = isPreRegistration ? IpdConstants.AdmissionStatus.PreAdmit : IpdConstants.AdmissionStatus.Admitted;

                        var admittedAt = request.AdmittedAt ?? now;
                        var admission = new Admission
                        {
                            AdmissionId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            PatientId = patient!.PatientId!,
                            EncounterId = null,
                            PrimaryDoctorId = request.PrimaryDoctorId,
                            AdmissionNo = admissionNo,
                            AdmissionToken = request.AdmissionToken?.Trim(),
                            AdmissionType = admissionType,
                            ReferralSource = string.IsNullOrWhiteSpace(request.ReferralSource) ? null : request.ReferralSource!.Trim().ToUpperInvariant(),
                            ReferralName = request.ReferralName?.Trim(),
                            ReferredByReferrerId = request.ReferredByReferrerId,
                            ReferringFacilityName = request.ReferringFacilityName?.Trim(),
                            ReferringFacilityType = string.IsNullOrWhiteSpace(request.ReferringFacilityType) ? null : request.ReferringFacilityType!.Trim().ToUpperInvariant(),
                            ReferringFacilityContact = request.ReferringFacilityContact?.Trim(),
                            AdmittedAt = admittedAt,
                            AdmittedBy = request.LoggedInUserName,
                            ExpectedDischargeAt = request.ExpectedDischargeAt,
                            StatusCode = initialStatus,
                            AdmissionReason = request.AdmissionReason,
                            Diagnosis = request.Diagnosis,
                            PayerType = payerType,
                            DepositExpected = request.DepositExpected,
                            EnableIpdBilling = request.EnableIpdBilling,
                            ClientRequestId = request.ClientRequestId,
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                            UpdatedAt = now,
                            UpdatedBy = request.LoggedInUserName,
                        };
                        _context.Admission.Add(admission);

                        // ── Open an IPD billing encounter so charges/day-wise bills accrue to the stay ──
                        if (request.EnableIpdBilling)
                        {
                            var encounter = new Encounter
                            {
                                EncounterId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                PatientId = patient.PatientId,
                                EncounterTypeCode = BillingConstants.EncounterType.Ipd,
                                SourceType = "Admission",
                                SourceId = admission.AdmissionId,
                                PrimaryDoctorId = request.PrimaryDoctorId,
                                StatusCode = BillingConstants.EncounterStatus.Open,
                                CreatedAt = now,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = now,
                                UpdatedBy = request.LoggedInUserName,
                            };
                            _context.Encounter.Add(encounter);
                            admission.EncounterId = encounter.EncounterId;
                        }

                        // ── Status-transition log (also the KPI source for BOR/turnaround/discharge-TAT) ──
                        _context.AdmissionStatusHistory.Add(new AdmissionStatusHistory
                        {
                            HistoryId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            AdmissionId = admission.AdmissionId,
                            FromStatus = null,
                            ToStatus = initialStatus,
                            ChangedAt = now,
                            ChangedBy = request.LoggedInUserName,
                            Reason = isPreRegistration ? "Pre-registered" : "Admission created",
                        });

                        // ── Coverage detail: always for TPA/SCHEME, or whenever any detail is supplied ──
                        if (payerType != IpdConstants.PayerType.Cash
                            || !string.IsNullOrWhiteSpace(request.PayerName)
                            || !string.IsNullOrWhiteSpace(request.PolicyOrBeneficiaryNo)
                            || !string.IsNullOrWhiteSpace(request.PreAuthNo)
                            || !string.IsNullOrWhiteSpace(request.PackageCode)
                            || !string.IsNullOrWhiteSpace(request.EntitledRoomCategory)
                            || request.SanctionedAmount.HasValue)
                        {
                            _context.AdmissionCoverage.Add(new AdmissionCoverage
                            {
                                CoverageId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                AdmissionId = admission.AdmissionId,
                                PayerType = payerType,
                                PayerName = request.PayerName?.Trim(),
                                PolicyOrBeneficiaryNo = request.PolicyOrBeneficiaryNo?.Trim(),
                                PreAuthNo = request.PreAuthNo?.Trim(),
                                PackageCode = request.PackageCode?.Trim(),
                                SanctionedAmount = request.SanctionedAmount,
                                EntitledRoomCategory = string.IsNullOrWhiteSpace(request.EntitledRoomCategory) ? null : request.EntitledRoomCategory!.Trim().ToUpperInvariant(),
                                StatusCode = IpdConstants.CoverageStatus.Pending,
                                CreatedAt = now,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = now,
                                UpdatedBy = request.LoggedInUserName,
                            });
                        }

                        // ── Optional bed assignment at admit time ───────────────────────────────────
                        BedAssignment? bedAssignment = null;
                        if (request.BedId.HasValue)
                        {
                            var bed = await _context.BedMaster
                                .FirstOrDefaultAsync(b => b.BedId == request.BedId.Value && b.HospitalId == request.HospitalId, cancellationToken);
                            if (bed == null)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new AdmitPatientResponseModel { Success = false, Message = "Bed not found." };
                            }

                            bedAssignment = new BedAssignment
                            {
                                AssignmentId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                AdmissionId = admission.AdmissionId,
                                BedId = bed.BedId,
                                AssignedAt = now,
                                AssignedBy = request.LoggedInUserName,
                                DailyRateSnapshot = bed.BedDailyRateOverride ?? bed.WardRoomDailyRate,
                                StatusCode = IpdConstants.BedAssignmentStatus.Active,
                                CreatedAt = now,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = now,
                                UpdatedBy = request.LoggedInUserName,
                            };
                            _context.BedAssignment.Add(bedAssignment);
                        }

                        try
                        {
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        catch (DbUpdateException) when (request.BedId.HasValue)
                        {
                            // Filtered unique index backstop: another admit won the race for this bed.
                            await tx.RollbackAsync(cancellationToken);
                            return new AdmitPatientResponseModel { Success = false, Message = "That bed is already occupied by another patient." };
                        }

                        await tx.CommitAsync(cancellationToken);

                        return new AdmitPatientResponseModel
                        {
                            Success = true,
                            Message = isPreRegistration ? $"Pre-registered. {admissionNo}" : $"Admitted. {admissionNo}",
                            AdmissionId = admission.AdmissionId,
                            AdmissionNo = admissionNo,
                            PatientId = patient.PatientId,
                            IsNewPatient = isNewPatient,
                            AdmittedAt = admittedAt,
                            WasExisting = !isNewPatient,
                            StatusCode = initialStatus,
                            EncounterId = admission.EncounterId,
                            PayerType = payerType,
                            BedId = bedAssignment?.BedId,
                            BedAssignmentId = bedAssignment?.AssignmentId,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new AdmitPatientResponseModel { Success = false, Message = "Error admitting patient." };
                    }
                });
            }
            catch (Exception)
            {
                return new AdmitPatientResponseModel { Success = false, Message = "Error admitting patient." };
            }
        }

        // Idempotent replay: echo back the admission already created for this ClientRequestId.
        private async Task<AdmitPatientResponseModel> BuildReplayResponseAsync(Admission admission, CancellationToken cancellationToken)
        {
            var activeBed = await _context.BedAssignment
                .Where(b => b.AdmissionId == admission.AdmissionId && b.StatusCode == IpdConstants.BedAssignmentStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            return new AdmitPatientResponseModel
            {
                Success = true,
                Message = admission.StatusCode == IpdConstants.AdmissionStatus.PreAdmit ? $"Pre-registered. {admission.AdmissionNo}" : $"Admitted. {admission.AdmissionNo}",
                AdmissionId = admission.AdmissionId,
                AdmissionNo = admission.AdmissionNo,
                PatientId = admission.PatientId,
                IsNewPatient = false,
                AdmittedAt = admission.AdmittedAt,
                WasExisting = true,
                StatusCode = admission.StatusCode,
                EncounterId = admission.EncounterId,
                PayerType = admission.PayerType,
                BedId = activeBed?.BedId,
                BedAssignmentId = activeBed?.AssignmentId,
            };
        }

        private static string? NormalizePayerType(string? payerType)
        {
            if (string.IsNullOrWhiteSpace(payerType)) return IpdConstants.PayerType.Cash;
            var v = payerType.Trim().ToUpperInvariant();
            return IpdConstants.PayerType.All.Contains(v) ? v : null;
        }

        private static string? NormalizeType(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return null;
            var v = t.Trim().ToUpperInvariant().Replace(" ", "").Replace("_", "");
            return v switch
            {
                "EMERGENCY" => "EMERGENCY",
                "ELECTIVE" or "PLANNED" => "ELECTIVE",
                "DAYCARE" => "DAYCARE",
                "LAMA" => "LAMA",
                _ => t.Trim().ToUpperInvariant(),
            };
        }

        // Emergency/casualty fallback when no name is given (or known) — PatientRegistration.FullName
        // is NOT NULL at the DB level, so a placeholder is required regardless; backfilled later.
        private static string SynthesizeUnknownName(string? sex, short? age)
        {
            var sexLabel = sex?.Trim().ToUpperInvariant() switch
            {
                "M" or "MALE" => "Male",
                "F" or "FEMALE" => "Female",
                _ => "Patient",
            };
            var ageSuffix = age.HasValue ? $", ~{age.Value}y" : "";
            return $"Unknown {sexLabel}{ageSuffix}";
        }

        // Copy any provided demographic field onto the patient (skip nulls so an edit never wipes data).
        private static void ApplyDemographics(PatientRegistration p, AdmitPatientRequestModel r)
        {
            if (!string.IsNullOrWhiteSpace(r.FullName)) p.FullName = r.FullName;
            if (!string.IsNullOrWhiteSpace(r.Mobile)) p.Mobile = r.Mobile;
            if (r.Age.HasValue) 
            {
                p.Age = r.Age;
                if (!string.IsNullOrEmpty(r.AgeUnit))
                {
                    p.AgeUnit = r.AgeUnit;
                }
            }
            if (r.DateOfBirth.HasValue) p.DateOfBirth = r.DateOfBirth;
            if (!string.IsNullOrWhiteSpace(r.Sex)) p.Sex = r.Sex;
            if (!string.IsNullOrWhiteSpace(r.BloodGroup)) p.BloodGroup = r.BloodGroup;
            if (!string.IsNullOrWhiteSpace(r.Religion)) p.Religion = r.Religion;
            if (!string.IsNullOrWhiteSpace(r.Nationality)) p.Nationality = r.Nationality;

            if (!string.IsNullOrWhiteSpace(r.FlatHouse)) p.FlatHouse = r.FlatHouse;
            if (!string.IsNullOrWhiteSpace(r.Street)) p.Street = r.Street;
            if (!string.IsNullOrWhiteSpace(r.AddressLine)) p.AddressLine = r.AddressLine;
            if (!string.IsNullOrWhiteSpace(r.Block)) p.Block = r.Block;
            if (!string.IsNullOrWhiteSpace(r.City)) p.City = r.City;
            if (!string.IsNullOrWhiteSpace(r.District)) p.District = r.District;
            if (!string.IsNullOrWhiteSpace(r.State)) p.State = r.State;
            if (!string.IsNullOrWhiteSpace(r.Pincode)) p.Pincode = r.Pincode;
            if (!string.IsNullOrWhiteSpace(r.Country)) p.Country = r.Country;

            if (!string.IsNullOrWhiteSpace(r.AlternateMobile)) p.AlternateMobile = r.AlternateMobile;
            if (!string.IsNullOrWhiteSpace(r.Email)) p.Email = r.Email;
            if (!string.IsNullOrWhiteSpace(r.EmergencyContactName)) p.EmergencyContactName = r.EmergencyContactName;
            if (!string.IsNullOrWhiteSpace(r.EmergencyContactRelation)) p.EmergencyContactRelation = r.EmergencyContactRelation;
            if (!string.IsNullOrWhiteSpace(r.EmergencyContactPhone)) p.EmergencyContactPhone = r.EmergencyContactPhone;

            if (!string.IsNullOrWhiteSpace(r.AadhaarNumber)) p.AadhaarNumber = r.AadhaarNumber;
            if (!string.IsNullOrWhiteSpace(r.PanNumber)) p.PanNumber = r.PanNumber;
            if (!string.IsNullOrWhiteSpace(r.AbhaId)) p.AbhaId = r.AbhaId;
        }

        private string GenerateUhid()
        {
            string newId;
            var rng = RandomNumberGenerator.Create();
            do
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                int num = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 100000000;
                newId = $"PTID{num:D8}";
            }
            while (_context.PatientRegistrations.Any(p => p.PatientId == newId));
            return newId;
        }
    }
}
