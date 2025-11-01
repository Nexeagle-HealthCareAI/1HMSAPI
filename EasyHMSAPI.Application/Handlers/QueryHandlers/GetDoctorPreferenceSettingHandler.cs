using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDoctorPreferenceSettingHandler : IRequestHandler<GetDoctorPreferenceSettingRequestModel, GetDoctorPreferenceSettingResponseModel>
    {
        private readonly AppDbContext _context;
        public GetDoctorPreferenceSettingHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDoctorPreferenceSettingResponseModel> Handle(GetDoctorPreferenceSettingRequestModel request, CancellationToken cancellationToken)
        {
            var preference = await _context.DoctorSectionPreferences.FirstOrDefaultAsync(p => p.DoctorId == request.DoctorId, cancellationToken);
            if (preference == null)
            {
                return new GetDoctorPreferenceSettingResponseModel
                {
                    Success = false,
                    Message = "Doctor preference setting not found.",
                    Preference = null
                };
            }
            return new GetDoctorPreferenceSettingResponseModel
            {
                Success = true,
                Message = "Doctor preference setting fetched successfully.",
                Preference = preference
            };
        }
    }
}