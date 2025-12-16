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
        public DeletePersonalizedDataHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<DeletePersonalizedDataResponseModel> Handle(DeletePersonalizedDataRequestModel request, CancellationToken cancellationToken)
        {
            var existingDoctor = await _dbContext.Doctors
               .Where(x => x.DoctorID == request.DoctorId)
               .FirstOrDefaultAsync(cancellationToken);
            if (existingDoctor == null)
            {
                return new DeletePersonalizedDataResponseModel { Message = "Doctor not found." };
            }

            var existingHospital = await _dbContext.Hospitals
                .Where(x => x.HospitalID == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingHospital == null)
            {
                return new DeletePersonalizedDataResponseModel { Message = "Hospital not found." };
            }

            var existing = await _dbContext.LookupPersonals
                .FirstOrDefaultAsync(lp => lp.PersonalId == request.PersonalId && lp.DoctorID == request.DoctorId, cancellationToken);
            if (existing == null)
            {
                return new DeletePersonalizedDataResponseModel { Message = "Not Found" };
            }

            _dbContext.LookupPersonals.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new DeletePersonalizedDataResponseModel { Message = "Success" };
        }
    }
}