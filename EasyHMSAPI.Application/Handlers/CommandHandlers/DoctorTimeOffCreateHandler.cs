using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Data.Enums;
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
                var doctorWithUser = await (from d in _context.Doctors
                                             join u in _context.Users on d.UserID equals u.UserID
                                             where d.DoctorID == request.DoctorId && u.UserStatusId != (int)UserStatusEnum.Revoked
                                             select new { d.DoctorID, u.UserID }).FirstOrDefaultAsync(cancellationToken);
                if (doctorWithUser == null)
                {
                    resp.Success = false;
                    resp.Message = "Doctor not found or user is revoked";
                    resp.Errors.Add($"Doctor {request.DoctorId} does not exist or user is revoked");
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
