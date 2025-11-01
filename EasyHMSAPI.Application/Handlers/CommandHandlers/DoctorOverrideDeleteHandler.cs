using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorOverrideDeleteHandler : IRequestHandler<DoctorOverrideDeleteRequestModel, DoctorOverrideDeleteResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorOverrideDeleteHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorOverrideDeleteResponseModel> Handle(DoctorOverrideDeleteRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new DoctorOverrideDeleteResponseModel { OverrideId = request.OverrideId };
            try
            {
                var entity = await _context.DoctorShiftOverrides.FirstOrDefaultAsync(o => o.OverrideID == request.OverrideId, cancellationToken);
                if (entity == null)
                {
                    resp.Success = false;
                    resp.Message = "Override not found";
                    return resp;
                }
                _context.DoctorShiftOverrides.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);
                resp.Success = true;
                resp.Message = "Override deleted";
                return resp;
            }
            catch (Exception ex)
            {
                resp.Success = false;
                resp.Message = "Failed to delete override";
                resp.Errors.Add(ex.Message);
                return resp;
            }
        }
    }
}
