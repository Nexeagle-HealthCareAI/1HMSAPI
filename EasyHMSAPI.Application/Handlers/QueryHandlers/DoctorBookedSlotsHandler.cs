using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorBookedSlotsHandler : IRequestHandler<DoctorBookedSlotsRequestModel, DoctorBookedSlotsResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorBookedSlotsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorBookedSlotsResponseModel> Handle(DoctorBookedSlotsRequestModel request, CancellationToken cancellationToken)
        {
            var bookedSlots = await _context.Appointments
                .Where(a => a.DoctorId == request.DoctorId && a.ApptDate.Date == request.Date.Date)
                .Select(a => a.StartAt.TimeOfDay)
                .ToListAsync(cancellationToken);

            return new DoctorBookedSlotsResponseModel
            {
                DoctorId = request.DoctorId,
                Date = request.Date.Date,
                BookedSlots = bookedSlots
            };
        }
    }
}
