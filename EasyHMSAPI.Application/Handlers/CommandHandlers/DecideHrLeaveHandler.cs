using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DecideHrLeaveHandler : IRequestHandler<DecideHrLeaveRequestModel, DecideHrLeaveResponseModel>
    {
        private readonly AppDbContext _context;

        public DecideHrLeaveHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DecideHrLeaveResponseModel> Handle(DecideHrLeaveRequestModel request, CancellationToken cancellationToken)
        {
            var leave = await _context.HrLeaveRequest.FindAsync(new object[] { request.LeaveId }, cancellationToken);
            if (leave == null)
            {
                return new DecideHrLeaveResponseModel
                {
                    Success = false,
                    Message = "Leave request not found.",
                    LeaveId = request.LeaveId,
                    Status = request.Status
                };
            }

            leave.Status = request.Status;
            await _context.SaveChangesAsync(cancellationToken);

            return new DecideHrLeaveResponseModel
            {
                Success = true,
                Message = "Leave status updated successfully.",
                LeaveId = request.LeaveId,
                Status = request.Status
            };
        }
    }
}
