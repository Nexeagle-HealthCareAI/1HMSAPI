using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetMedicalSpecialitiesHandler : IRequestHandler<GetMedicalSpecialitiesRequestModel, GetMedicalSpecialitiesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetMedicalSpecialitiesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMedicalSpecialitiesResponseModel> Handle(GetMedicalSpecialitiesRequestModel request, CancellationToken cancellationToken)
        {
            var items = await _context.MedicalSpecialities
                .Where(s => s.IsActive)
                .OrderBy(s => s.QualificationTypeCode).ThenBy(s => s.SortOrder)
                .Select(s => new MedicalSpecialityItem
                {
                    SpecialityId = s.SpecialityId,
                    QualificationTypeCode = s.QualificationTypeCode,
                    QualificationTypeName = s.QualificationType.Name,
                    Name = s.Name,
                    PatientFacingName = s.PatientFacingName,
                    PatientFacingCategory = s.PatientFacingCategory,
                    SortOrder = s.SortOrder
                })
                .ToListAsync(cancellationToken);

            return new GetMedicalSpecialitiesResponseModel { Items = items };
        }
    }
}
