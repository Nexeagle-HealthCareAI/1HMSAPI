using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetConsentTemplatesHandler : IRequestHandler<GetConsentTemplatesRequestModel, GetConsentTemplatesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetConsentTemplatesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetConsentTemplatesResponseModel> Handle(GetConsentTemplatesRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetConsentTemplatesResponseModel { Success = false, Message = "HospitalId is required." };

                var query = _context.ConsentTemplate.Where(t => t.HospitalId == request.HospitalId);
                if (request.ActiveOnly)
                    query = query.Where(t => t.IsActive);
                if (!string.IsNullOrWhiteSpace(request.TypeCode))
                    query = query.Where(t => t.TypeCode == request.TypeCode.Trim().ToUpper());
                if (!string.IsNullOrWhiteSpace(request.Language))
                    query = query.Where(t => t.Language == request.Language.Trim().ToUpper());

                var templates = await query
                    .OrderBy(t => t.TypeCode).ThenByDescending(t => t.Version)
                    .Select(t => new ConsentTemplateItem
                    {
                        ConsentTemplateId = t.ConsentTemplateId,
                        TypeCode = t.TypeCode,
                        Title = t.Title,
                        Language = t.Language,
                        Version = t.Version,
                        BodyHtml = t.BodyHtml,
                        IsActive = t.IsActive,
                    })
                    .ToListAsync(cancellationToken);

                return new GetConsentTemplatesResponseModel { Success = true, Templates = templates };
            }
            catch (Exception)
            {
                return new GetConsentTemplatesResponseModel { Success = false, Message = "Error loading consent templates." };
            }
        }
    }
}
