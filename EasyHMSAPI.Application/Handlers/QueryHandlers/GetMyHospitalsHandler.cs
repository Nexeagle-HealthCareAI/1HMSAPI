using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Lists every hospital the user is a member of, with chain context, for the switcher.</summary>
    public class GetMyHospitalsHandler : IRequestHandler<GetMyHospitalsRequestModel, GetMyHospitalsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetMyHospitalsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMyHospitalsResponseModel> Handle(GetMyHospitalsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.UserId == Guid.Empty)
                    return new GetMyHospitalsResponseModel { Success = false, Message = "UserId is required." };

                var items = await (
                    from hu in _context.HospitalUsers
                    where hu.UserID == request.UserId
                    join h in _context.Hospitals on hu.HospitalID equals h.HospitalID
                    join c in _context.HospitalChains on h.ChainId equals c.ChainId into chains
                    from c in chains.DefaultIfEmpty()
                    orderby hu.IsPrimary descending, h.Name
                    select new MyHospitalItem
                    {
                        HospitalId = h.HospitalID,
                        Name = h.Name,
                        City = h.City,
                        IsPrimary = hu.IsPrimary,
                        EmployeeId = hu.EmployeeID,
                        ChainId = h.ChainId,
                        ChainName = c != null ? c.Name : null,
                        IsChainOwner = c != null && c.OwnerUserId == request.UserId,
                    }).ToListAsync(cancellationToken);

                return new GetMyHospitalsResponseModel { Success = true, Hospitals = items };
            }
            catch (Exception)
            {
                return new GetMyHospitalsResponseModel { Success = false, Message = "Error loading hospitals." };
            }
        }
    }
}
