using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPreferredMedicinesHandler : IRequestHandler<GetPreferredMedicinesRequestModel, List<GetPreferredMedicineResponseModel>>
    {
        private readonly AppDbContext _dbContext;
        public GetPreferredMedicinesHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<GetPreferredMedicineResponseModel>> Handle(GetPreferredMedicinesRequestModel request, CancellationToken cancellationToken)
        {
            var existingDoctor = await _dbContext.Doctors
              .Where(x => x.DoctorID == request.DoctorId)
              .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Doctor not found.");
            var existingHospital = await _dbContext.Hospitals
                .Where(x => x.HospitalID == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Hospital not found.");

            var list = await _dbContext.DoctorPreferredMedicines
                .AsNoTracking()
                .Where(d => d.DoctorId == request.DoctorId && d.HospitalId == request.HospitalId && d.IsActive)
                .Select(d => new GetPreferredMedicineResponseModel
                {
                    PrefferedId = d.PreferrredId,
                    GenericName = d.GenericName,
                    BrandName = d.BrandName ?? string.Empty,
                    Form = d.Form ?? string.Empty,
                    StrengthValue = d.StrengthValue ?? string.Empty,
                    StrengthUnit = d.StrengthUnit ?? string.Empty,
                    Route = d.Route ?? string.Empty,
                    Dose = d.Dose ?? string.Empty,
                    Indication = d.Indication ?? string.Empty,
                    Notes = d.Notes ?? string.Empty,
                    MedicineId = d.MedicineId ?? string.Empty,
                    UsageCount = d.UsageCount,
                    LastModifiedAt = d.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return list;
        }
    }
}