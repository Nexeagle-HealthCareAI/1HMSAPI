using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeletePersonalizedDataHandler : IRequestHandler<DeletePersonalizedDataRequestModel, DeletePersonalizedDataResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public DeletePersonalizedDataHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _dbContext = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<DeletePersonalizedDataResponseModel> Handle(DeletePersonalizedDataRequestModel request, CancellationToken cancellationToken)
        {
            DeletePersonalizedDataResponseModel response = new();
            try
            {
                var existingDoctor = await _dbContext.Doctors
                 .Where(x => x.DoctorID == request.DoctorId)
                 .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                    return response;
                }

                var existingHospital = await _dbContext.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital == null)
                {
                    response.Message = "Hospital not found.";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                var existing = await _dbContext.LookupPersonals
                    .FirstOrDefaultAsync(lp => lp.PersonalId == request.PersonalId && lp.DoctorID == request.DoctorId && lp.HospitalID == request.HospitalId, cancellationToken);
                if (existing == null)
                {
                    response.Success = false;
                    response.Message = "Personalized data not found.";
                }
                else
                {
                    _dbContext.LookupPersonals.Remove(existing);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "Personalized data deleted successfully.";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}