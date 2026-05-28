using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteChargeMasterHandler : IRequestHandler<DeleteChargeMasterRequestModel, DeleteChargeMasterResponseModel>
    {
        private readonly AppDbContext _context;

        public DeleteChargeMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DeleteChargeMasterResponseModel> Handle(DeleteChargeMasterRequestModel request, CancellationToken cancellationToken)
        {
            var response = new DeleteChargeMasterResponseModel();

            var charge = await _context.ChargeMaster
                .FirstOrDefaultAsync(x => x.ChargeId == request.ChargeId && x.HospitalId == request.HospitalId, cancellationToken);

            if (charge == null)
            {
                response.IsSucess = false;
                response.Message = "Invalid chargeId";
            }
            else
            {
                _context.ChargeMaster.Remove(charge);
                await _context.SaveChangesAsync(cancellationToken);

                response.IsSucess = true;
                response.Message = "Charge deleted successfully";
            }

            return response;
        }
    }
}
