using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorTimeOffDeleteHandler : IRequestHandler<DoctorTimeOffDeleteRequestModel, DoctorTimeOffDeleteResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorTimeOffDeleteHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorTimeOffDeleteResponseModel> Handle(DoctorTimeOffDeleteRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new DoctorTimeOffDeleteResponseModel { TimeOffId = request.TimeOffId };
            try
            {
                var entity = await _context.DoctorTimeOffs.FirstOrDefaultAsync(t => t.TimeOffID == request.TimeOffId, cancellationToken);
                if (entity == null)
                {
                    resp.Success = false;
                    resp.Message = "Time-off not found";
                    return resp;
                }
                _context.DoctorTimeOffs.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);
                resp.Success = true;
                resp.Message = "Time-off deleted";
                return resp;
            }
            catch (Exception ex)
            {
                resp.Success = false;
                resp.Message = "Failed to delete time-off";
                resp.Errors.Add(ex.Message);
                return resp;
            }
        }
    }
}
