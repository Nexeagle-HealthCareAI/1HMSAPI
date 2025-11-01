using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientProfileHandler : IRequestHandler<GetPatientProfileRequestModel, GetPatientProfileResponseModel?>
    {
        private readonly AppDbContext _context;
        public GetPatientProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientProfileResponseModel?> Handle(GetPatientProfileRequestModel request, CancellationToken cancellationToken)
        {
            var patient = await _context.PatientRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalId == request.HospitalId && x.PatientId == request.PatientId, cancellationToken);
            if (patient == null)
                return null;
            return new GetPatientProfileResponseModel
            {
                RegistrationId = patient.RegistrationId,
                HospitalId = patient.HospitalId,
                PatientId = patient.PatientId,
                FullName = patient.FullName,
                Mobile = patient.Mobile,
                AgeYears = patient.AgeYears,
                Sex = patient.Sex,
                AddressLine1 = patient.AddressLine,
                City = patient.City,
                State = patient.State,
                Country = patient.Country,
                Pincode = patient.Pincode,
                InsuranceId = patient.InsuranceId,
                RegisteredBy = patient.RegisteredBy
            };
        }
    }
}