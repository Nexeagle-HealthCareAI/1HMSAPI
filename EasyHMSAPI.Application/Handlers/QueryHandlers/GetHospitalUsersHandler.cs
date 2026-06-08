using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalUsersHandler : IRequestHandler<GetHospitalUsersRequestModel, GetHospitalUsersResponseModel?>
    {
        private readonly AppDbContext _context;

        public GetHospitalUsersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalUsersResponseModel?> Handle(GetHospitalUsersRequestModel request, CancellationToken cancellationToken)
        {
            // Prefer the user's primary hospital so this legacy single-hospital endpoint is deterministic
            // (multi-hospital users get the full list via /hospitals/mine for the switcher).
            var hospitalUser = await _context.HospitalUsers
                .Where(hu => hu.UserID == request.UserId)
                .OrderByDescending(hu => hu.IsPrimary)
                .FirstOrDefaultAsync(cancellationToken);

            if (hospitalUser == null)
            {
                return null;
            }

            return new GetHospitalUsersResponseModel
            {
                HospitalUserId = hospitalUser.HospitalUserID,
                HospitalId = hospitalUser.HospitalID,
                UserId = hospitalUser.UserID,
                EmployeeID = hospitalUser.EmployeeID,
                IsPrimary = hospitalUser.IsPrimary.ToString(),
                CreatedAt = hospitalUser.CreatedAt
            };
        }
    }
}