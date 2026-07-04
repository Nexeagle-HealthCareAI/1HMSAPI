using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePatientProfileHandler : IRequestHandler<UpdatePatientProfileRequestModel, UpdatePatientProfileResponseModel>
    {
        private readonly AppDbContext _context;
        public UpdatePatientProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdatePatientProfileResponseModel> Handle(UpdatePatientProfileRequestModel request, CancellationToken cancellationToken)
        {
            var patient = await _context.PatientRegistrations
                .FirstOrDefaultAsync(x => x.HospitalId == request.HospitalId && x.PatientId == request.PatientId, cancellationToken);
            if (patient == null)
            {
                return new UpdatePatientProfileResponseModel { Success = false, Message = "Patient not found." };
            }

            // Update fields if provided
            if (!string.IsNullOrWhiteSpace(request.FullName)) patient.FullName = request.FullName;
            if (!string.IsNullOrWhiteSpace(request.Mobile)) patient.Mobile = request.Mobile;
            if (request.Age.HasValue) 
            {
                patient.Age = request.Age;
                if (!string.IsNullOrEmpty(request.AgeUnit))
                {
                    patient.AgeUnit = request.AgeUnit;
                }
            }
            if (!string.IsNullOrWhiteSpace(request.Sex)) patient.Sex = request.Sex;
            if (!string.IsNullOrWhiteSpace(request.AddressLine1)) patient.AddressLine = request.AddressLine1;
            if (!string.IsNullOrWhiteSpace(request.City)) patient.City = request.City;
            if (!string.IsNullOrWhiteSpace(request.State)) patient.State = request.State;
            if (!string.IsNullOrWhiteSpace(request.Country)) patient.Country = request.Country;
            if (!string.IsNullOrWhiteSpace(request.Pincode)) patient.Pincode = request.Pincode;
            if (!string.IsNullOrWhiteSpace(request.InsuranceId)) patient.InsuranceId = request.InsuranceId;
            // PaymentMode is intentionally not persisted here — it's per-visit (Appointment.PaymentMode),
            // not a patient-registration attribute; this request field is unused/legacy.
            // Clinical/contact fields — null = leave unchanged, "" = explicitly clear (e.g. correcting allergies).
            if (request.BloodGroup != null) patient.BloodGroup = request.BloodGroup;
            if (request.Allergies != null) patient.Allergies = request.Allergies;
            if (request.Email != null) patient.Email = request.Email;
            if (request.EmergencyContactName != null) patient.EmergencyContactName = request.EmergencyContactName;
            if (request.EmergencyContactPhone != null) patient.EmergencyContactPhone = request.EmergencyContactPhone;
            if (request.EmergencyContactRelation != null) patient.EmergencyContactRelation = request.EmergencyContactRelation;
            if (request.Block != null) patient.Block = request.Block;
            if (request.AlternateMobile != null) patient.AlternateMobile = request.AlternateMobile;
            if (request.GuardianName != null) patient.GuardianName = request.GuardianName;
            if (request.GuardianRelation != null) patient.GuardianRelation = request.GuardianRelation;

            await _context.SaveChangesAsync(cancellationToken);
            return new UpdatePatientProfileResponseModel { Success = true, Message = "Patient profile updated successfully." };
        }
    }
}