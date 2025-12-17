using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeletePreferredMedicineHandler : IRequestHandler<DeletePreferredMedicineRequestModel, DeletePreferredMedicineResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        public DeletePreferredMedicineHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<DeletePreferredMedicineResponseModel> Handle(DeletePreferredMedicineRequestModel request, CancellationToken cancellationToken)
        {
            DeletePreferredMedicineResponseModel response = new()
            {
                Success = false,
            };
            try
            {
                var existingDoctor = await _dbContext.Doctors
                 .Where(x => x.DoctorID == request.DoctorId)
                 .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                }

                var existingHospital = await _dbContext.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Hospital not found.");
                if (existingHospital == null)
                {
                    response.Message = "Hospital not found.";
                }
                else
                {
                    if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                    {
                        response.Message = "Doctor is not associated with the specified hospital.";
                    }
                }

                var preferredMedicine = await _dbContext.DoctorPreferredMedicines
                    .Where(x => x.PreferrredId == request.PreferredId && x.DoctorId == request.DoctorId && x.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (preferredMedicine is not null)
                {
                    _dbContext.DoctorPreferredMedicines.Remove(preferredMedicine);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    response.Success = true;
                    response.Message = "Preferred medicine deleted successfully.";

                }
                else
                {
                    response.Message = "Preferred medicine not found.";
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }

            return response;
        }
    }
}
