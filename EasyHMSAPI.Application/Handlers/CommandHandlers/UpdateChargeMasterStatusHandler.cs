using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateChargeMasterStatusHandler : IRequestHandler<UpdateChargeMasterStatusRequestModel, UpdateChargeMasterStatusResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateChargeMasterStatusHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateChargeMasterStatusResponseModel> Handle(UpdateChargeMasterStatusRequestModel request, CancellationToken cancellationToken)
        {
            var response = new UpdateChargeMasterStatusResponseModel();

            var charge = await _context.ChargeMaster
                .FirstOrDefaultAsync(x => x.ChargeId == request.ChargeId && x.HospitalId == request.HospitalId, cancellationToken);

            if (charge == null)
            {
                response.IsSucess = false;
                response.Message = "Charge not found.";
            }
            else
            {
                charge.IsActive = request.IsActive;
                charge.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                response.IsSucess = true;
                response.Message = "Charge status updated successfully.";
            }

            return response;
        }
    }
}
