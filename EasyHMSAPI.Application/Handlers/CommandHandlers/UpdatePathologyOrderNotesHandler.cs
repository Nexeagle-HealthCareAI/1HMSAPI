using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Lets a tech correct an order's clinical notes or STAT flag after it's already been placed --
    // freely re-savable, same no-lock philosophy as SaveOrderReportFieldsHandler.
    public class UpdatePathologyOrderNotesHandler : IRequestHandler<UpdatePathologyOrderNotesCommand, bool>
    {
        private readonly AppDbContext _context;

        public UpdatePathologyOrderNotesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdatePathologyOrderNotesCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.PathologyOrder
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);
            if (order == null)
            {
                return false;
            }

            order.Notes = request.Notes;
            order.IsStat = request.IsStat;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            _context.PathologyOrder.Update(order);

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
