using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Loads a returning patient's full demographics plus their admission history (newest first)
    /// for the IPD admission screen. Aadhaar is masked before it leaves the server.
    /// </summary>
    public class GetPatientAdmissionsHandler : IRequestHandler<GetPatientAdmissionsRequestModel, GetPatientAdmissionsResponseModel>
    {
        private const int DischargePreviewLength = 240;
        private readonly AppDbContext _context;

        public GetPatientAdmissionsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientAdmissionsResponseModel> Handle(GetPatientAdmissionsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.PatientId))
                    return new GetPatientAdmissionsResponseModel { Success = false, Message = "HospitalId and PatientId are required." };

                var p = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(x => x.PatientId == request.PatientId && x.HospitalId == request.HospitalId, cancellationToken);

                if (p == null)
                    return new GetPatientAdmissionsResponseModel { Success = false, Message = "Patient not found." };

                var admissions = await _context.Admission
                    .Where(a => a.PatientId == request.PatientId && a.HospitalId == request.HospitalId)
                    .OrderByDescending(a => a.AdmittedAt)
                    .ToListAsync(cancellationToken);

                return new GetPatientAdmissionsResponseModel
                {
                    Success = true,
                    Patient = new AdmissionPatientDetail
                    {
                        PatientId = p.PatientId!,
                        FullName = p.FullName,
                        Mobile = p.Mobile,
                        Age = p.Age,
                        AgeUnit = p.AgeUnit,
                        DateOfBirth = p.DateOfBirth,
                        Sex = p.Sex,
                        BloodGroup = p.BloodGroup,
                        Religion = p.Religion,
                        Nationality = p.Nationality,
                        FlatHouse = p.FlatHouse,
                        Street = p.Street,
                        AddressLine = p.AddressLine,
                        Block = p.Block,
                        City = p.City,
                        District = p.District,
                        State = p.State,
                        Pincode = p.Pincode,
                        Country = p.Country,
                        AlternateMobile = p.AlternateMobile,
                        Email = p.Email,
                        EmergencyContactName = p.EmergencyContactName,
                        EmergencyContactRelation = p.EmergencyContactRelation,
                        EmergencyContactPhone = p.EmergencyContactPhone,
                        AadhaarMasked = MaskAadhaar(p.AadhaarNumber),
                        PanNumber = p.PanNumber,
                        AbhaId = p.AbhaId,
                    },
                    Admissions = admissions.Select(a => new AdmissionHistoryItem
                    {
                        AdmissionId = a.AdmissionId,
                        AdmissionNo = a.AdmissionNo,
                        AdmissionType = a.AdmissionType,
                        AdmittedAt = a.AdmittedAt,
                        DischargedAt = a.DischargedAt,
                        StatusCode = a.StatusCode,
                        AdmissionReason = a.AdmissionReason,
                        Diagnosis = a.Diagnosis,
                        DischargeNotesPreview = Preview(a.DischargeNotes),
                    }).ToList(),
                };
            }
            catch (Exception)
            {
                return new GetPatientAdmissionsResponseModel { Success = false, Message = "Error loading patient admissions." };
            }
        }

        private static string? MaskAadhaar(string? aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar)) return null;
            var digits = new string(aadhaar.Where(char.IsDigit).ToArray());
            if (digits.Length < 4) return "XXXX";
            return "XXXX-XXXX-" + digits[^4..];
        }

        private static string? Preview(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            var trimmed = notes.Trim();
            return trimmed.Length <= DischargePreviewLength ? trimmed : trimmed[..DischargePreviewLength] + "…";
        }
    }
}
