using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertOTPlanHandler : IRequestHandler<UpsertOTPlanRequestModel, UpsertOTPlanResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertOTPlanHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertOTPlanResponseModel> Handle(UpsertOTPlanRequestModel request, CancellationToken cancellationToken)
        {
            UpsertOTPlanResponseModel response = new() { Success = false };
            try
            {
                if (string.IsNullOrWhiteSpace(request.PlanName) || string.IsNullOrWhiteSpace(request.ProcedureName))
                {
                    response.Message = "PlanName and ProcedureName are required.";
                    return response;
                }

                if (request.OtPlanId.HasValue && request.OtPlanId != Guid.Empty)
                {
                    var existing = await _context.OTPlans
                        .FirstOrDefaultAsync(x => x.OtPlanId == request.OtPlanId && x.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                    {
                        response.Message = $"OT Plan with ID {request.OtPlanId} not found.";
                        return response;
                    }

                    existing.DepartmentId = request.DepartmentId;
                    existing.PlanName = request.PlanName.Trim();
                    existing.ProcedureName = request.ProcedureName.Trim();
                    existing.DefaultRoomCategory = request.DefaultRoomCategory;
                    existing.SuggestedIcuLevel = request.SuggestedIcuLevel;
                    existing.IsActive = request.IsActive;
                    existing.DisplayOrder = request.DisplayOrder;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "OT Plan updated successfully.";
                    response.OtPlanId = existing.OtPlanId;
                    response.UpdatedAt = existing.UpdatedAt;
                    response.UpdatedBy = existing.UpdatedBy;
                    return response;
                }

                var plan = new OTPlan
                {
                    OtPlanId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    DepartmentId = request.DepartmentId,
                    PlanName = request.PlanName.Trim(),
                    ProcedureName = request.ProcedureName.Trim(),
                    DefaultRoomCategory = request.DefaultRoomCategory,
                    SuggestedIcuLevel = request.SuggestedIcuLevel,
                    IsActive = request.IsActive,
                    DisplayOrder = request.DisplayOrder,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.OTPlans.Add(plan);
                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "OT Plan created successfully.";
                response.OtPlanId = plan.OtPlanId;
                response.UpdatedAt = plan.UpdatedAt;
                response.UpdatedBy = plan.UpdatedBy;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
