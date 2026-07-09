using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetOTPlansHandler : IRequestHandler<GetOTPlansRequestModel, GetOTPlansResponseModel>
    {
        private readonly AppDbContext _context;

        public GetOTPlansHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetOTPlansResponseModel> Handle(GetOTPlansRequestModel request, CancellationToken cancellationToken)
        {
            GetOTPlansResponseModel response = new() { Success = false };
            try
            {
                var query = _context.OTPlans
                    .Where(p => p.HospitalId == request.HospitalId);

                if (!request.IncludeInactive)
                    query = query.Where(p => p.IsActive);

                if (request.DepartmentId.HasValue && request.DepartmentId != Guid.Empty)
                    query = query.Where(p => p.DepartmentId == request.DepartmentId);

                var plans = await query
                    .OrderBy(p => p.DisplayOrder).ThenBy(p => p.PlanName)
                    .ToListAsync(cancellationToken);

                var departmentNames = await _context.Departments
                    .Where(d => plans.Select(p => p.DepartmentId).Contains(d.DepartmentID))
                    .ToDictionaryAsync(d => d.DepartmentID, d => d.Name, cancellationToken);

                var otPlanIds = plans.Select(p => p.OtPlanId).ToList();
                var links = await _context.OTPlanPackageTypes
                    .Where(l => otPlanIds.Contains(l.OtPlanId))
                    .ToListAsync(cancellationToken);

                var packageTypeIds = links.Select(l => l.PackageTypeId).Distinct().ToList();
                var packageTypesById = await _context.PackageTypes
                    .Where(pt => packageTypeIds.Contains(pt.PackageTypeId))
                    .ToDictionaryAsync(pt => pt.PackageTypeId, pt => new { pt.Name, pt.Price }, cancellationToken);

                var linksByPlan = links.ToLookup(l => l.OtPlanId);

                response.Plans = plans.Select(p => new OTPlanDataModel
                {
                    OtPlanId = p.OtPlanId,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.DepartmentId.HasValue && departmentNames.TryGetValue(p.DepartmentId.Value, out var name) ? name : null,
                    PackageTypes = linksByPlan[p.OtPlanId]
                        .Where(l => packageTypesById.ContainsKey(l.PackageTypeId))
                        .Select(l => new PackageTypeRefDataModel
                        {
                            PackageTypeId = l.PackageTypeId,
                            Name = packageTypesById[l.PackageTypeId].Name,
                            Price = packageTypesById[l.PackageTypeId].Price,
                        }).ToList(),
                    PlanName = p.PlanName,
                    ProcedureName = p.ProcedureName,
                    DefaultRoomCategory = p.DefaultRoomCategory,
                    SuggestedIcuLevel = p.SuggestedIcuLevel,
                    IsActive = p.IsActive,
                    DisplayOrder = p.DisplayOrder,
                    UpdatedAt = p.UpdatedAt,
                    UpdatedBy = p.UpdatedBy,
                }).ToList();
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
