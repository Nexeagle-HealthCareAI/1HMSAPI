using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPreferredMedicineHandler : IRequestHandler<UpsertPreferredMedicineRequestModel, UpsertPreferredMedicineResponseModel>
    {
        private readonly AppDbContext _dbContext;
        public UpsertPreferredMedicineHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UpsertPreferredMedicineResponseModel> Handle(UpsertPreferredMedicineRequestModel request, CancellationToken cancellationToken)
        {
            var existingDoctor = await _dbContext.Doctors
              .Where(x => x.DoctorID == request.DoctorId)
              .FirstOrDefaultAsync(cancellationToken);
            if (existingDoctor == null)
            {
                return new UpsertPreferredMedicineResponseModel { Message = "Doctor not found." };
            }

            var existingHospital = await _dbContext.Hospitals
                .Where(x => x.HospitalID == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingHospital == null)
            {
                return new UpsertPreferredMedicineResponseModel { Message = "Hospital not found." };
            }

            var existing = await _dbContext.DoctorPreferredMedicines
                .FirstOrDefaultAsync(dpm => dpm.DoctorId == request.DoctorId && dpm.HospitalId == request.HospitalId && dpm.MedicineId == request.Medicine.MedicineId, cancellationToken);

            if (existing != null)
            {
                existing.BrandName = request.Medicine.BrandName;
                existing.GenericName = request.Medicine.GenericName;
                existing.Form = request.Medicine.Form;
                existing.StrengthValue = request.Medicine.StrengthValue;
                existing.StrengthUnit = request.Medicine.StrengthUnit;
                existing.Route = request.Medicine.Route;
                existing.Dose = request.Medicine.Dose;
                existing.Indication = request.Medicine.Indication;
                existing.Notes = request.Medicine.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.HospitalId = request.HospitalId;

                _dbContext.DoctorPreferredMedicines.Update(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return new UpsertPreferredMedicineResponseModel { Message = "Success" };
            }

            var newMed = new DoctorPreferredMedicine
            {
                BrandName = request.Medicine.BrandName,
                GenericName = request.Medicine.GenericName,
                Form = request.Medicine.Form,
                StrengthValue = request.Medicine.StrengthValue,
                StrengthUnit = request.Medicine.StrengthUnit,
                Route = request.Medicine.Route,
                Dose = request.Medicine.Dose,
                Indication = request.Medicine.Indication,
                Notes = request.Medicine.Notes,
                MedicineId = request.Medicine.MedicineId,
                DoctorId = request.DoctorId,
                HospitalId = request.HospitalId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _dbContext.DoctorPreferredMedicines.AddAsync(newMed, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new UpsertPreferredMedicineResponseModel { Message = "Success" };
        }
    }
}