using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAbhaAccountsHandler : IRequestHandler<GetAbhaAccountsRequestModel, GetAbhaAccountsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAbhaAccountsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAbhaAccountsResponseModel> Handle(GetAbhaAccountsRequestModel request, CancellationToken cancellationToken)
        {
            var accounts = await _context.AbhaAccount
                .Where(a => a.HospitalId == request.HospitalId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AbhaAccountSummary
                {
                    AbhaAccountId = a.AbhaAccountId,
                    AbhaNumber = a.AbhaNumber,
                    AbhaAddress = a.AbhaAddress,
                    FullName = a.FullName,
                    Gender = a.Gender,
                    DateOfBirth = a.DateOfBirth,
                    Mobile = a.Mobile,
                    Source = a.Source,
                    LinkedPatientId = a.LinkedPatientId,
                    CreatedAt = a.CreatedAt,
                    CreatedBy = a.CreatedBy
                })
                .ToListAsync(cancellationToken);

            return new GetAbhaAccountsResponseModel { Success = true, Accounts = accounts };
        }
    }
}
