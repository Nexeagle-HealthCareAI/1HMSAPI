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
    /// Standalone — no billing encounter required.
    /// </summary>
    public class AdmitPatientHandler : IRequestHandler<AdmitPatientRequestModel, AdmitPatientResponseModel>
    {
        private const string StatusAdmitted = "ADMITTED";
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

                var now = DateTime.UtcNow;

                // ── Resolve the patient (existing UHID) or register a new one ──────────────
                PatientRegistration? patient = null;
                if (!string.IsNullOrWhiteSpace(request.PatientId))
                {
                    patient = await _context.PatientRegistrations
                        .FirstOrDefaultAsync(p => p.PatientId == request.PatientId && p.HospitalId == request.HospitalId, cancellationToken);
                }

                bool isNewPatient = patient == null;
                if (isNewPatient)
                {
                    if (string.IsNullOrWhiteSpace(request.FullName))
                        return new AdmitPatientResponseModel { Success = false, Message = "Patient name is required to register a new patient." };

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

                // ── Create the admission (own IPD number) ──────────────────────────────────
                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.Admission, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var admissionNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                numberSeries.UpdatedAt = now;
                numberSeries.UpdatedBy = request.LoggedInUserName;

                var admittedAt = request.AdmittedAt ?? now;
                var admission = new Admission
                {
                    AdmissionId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = patient!.PatientId!,
                    EncounterId = null,
                    PrimaryDoctorId = request.PrimaryDoctorId,
                    AdmissionNo = admissionNo,
                    AdmissionType = NormalizeType(request.AdmissionType),
                    ReferralSource = string.IsNullOrWhiteSpace(request.ReferralSource) ? null : request.ReferralSource!.Trim().ToUpperInvariant(),
                    ReferralName = request.ReferralName?.Trim(),
                    ReferredByReferrerId = request.ReferredByReferrerId,
                    AdmittedAt = admittedAt,
                    AdmittedBy = request.LoggedInUserName,
                    ExpectedDischargeAt = request.ExpectedDischargeAt,
                    StatusCode = StatusAdmitted,
                    AdmissionReason = request.AdmissionReason,
                    Diagnosis = request.Diagnosis,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Admission.Add(admission);

                await _context.SaveChangesAsync(cancellationToken);

                return new AdmitPatientResponseModel
                {
                    Success = true,
                    Message = $"Admitted. {admissionNo}",
                    AdmissionId = admission.AdmissionId,
                    AdmissionNo = admissionNo,
                    PatientId = patient.PatientId,
                    IsNewPatient = isNewPatient,
                    AdmittedAt = admittedAt,
                    WasExisting = !isNewPatient,
                };
            }
            catch (Exception)
            {
                return new AdmitPatientResponseModel { Success = false, Message = "Error admitting patient." };
            }
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
