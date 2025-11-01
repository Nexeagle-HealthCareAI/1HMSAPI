using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DoctorTimeOffCreateHandler : IRequestHandler<DoctorTimeOffCreateRequestModel, DoctorTimeOffCreateResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorTimeOffCreateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorTimeOffCreateResponseModel> Handle(DoctorTimeOffCreateRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new DoctorTimeOffCreateResponseModel();
            try
            {
                var doctorExists = await _context.Doctors.AnyAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
                if (!doctorExists)
                {
                    resp.Success = false;
                    resp.Message = "Doctor not found";
                    resp.Errors.Add($"Doctor {request.DoctorId} does not exist");
                    return resp;
                }

                var from = request.FromDate.Date;
                var to = request.ToDate.Date;
                if (to < from)
                {
                    resp.Success = false;
                    resp.Message = "toDate must be on or after fromDate";
                    return resp;
                }

                var entity = new DoctorTimeOff
                {
                    TimeOffID = Guid.NewGuid(),
                    DoctorID = request.DoctorId,
                    FromDate = from,
                    ToDate = to,
                    Reason = request.Reason,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DoctorTimeOffs.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
                resp.Message = "Time-off saved";
                resp.TimeOffId = entity.TimeOffID;
                resp.CreatedAt = entity.CreatedAt;
                return resp;
            }
            catch (Exception ex)
            {
                resp.Success = false;
                resp.Message = "Failed to save time-off";
                resp.Errors.Add(ex.Message);
                return resp;
            }
        }
    }
}
