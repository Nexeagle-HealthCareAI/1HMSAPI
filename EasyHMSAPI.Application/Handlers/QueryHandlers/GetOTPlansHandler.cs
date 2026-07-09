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

                response.Plans = plans.Select(p => new OTPlanDataModel
                {
                    OtPlanId = p.OtPlanId,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.DepartmentId.HasValue && departmentNames.TryGetValue(p.DepartmentId.Value, out var name) ? name : null,
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
