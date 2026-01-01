using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientsByHospitalIdHandler : IRequestHandler<GetPatientsByHospitalIdRequestModel, GetPatientsByHospitalIdResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientsByHospitalIdHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientsByHospitalIdResponseModel> Handle(GetPatientsByHospitalIdRequestModel request, CancellationToken cancellationToken)
        {
            GetPatientsByHospitalIdResponseModel response = new()
            {
                HospitalId = request.HospitalId,
                Success = false,
            };
            try
            {
                var existingHospital = await _context.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .Select(y => new
                    {
                        y.HospitalID,
                        y.Name
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital is not null)
                {
                    var patients = await _context.PatientRegistrations
                        .Where(p => p.HospitalId == request.HospitalId)
                        .Select(p => new PatientDataModel
                        {
                            PatientId = p.PatientId,
                            Name = p.FullName,
                            Age = p.AgeYears,
                            Sex = p.Sex,
                            Contact = p.Mobile,
                            AddressLine = p.AddressLine,
                            City = p.City,
                            State = p.State,
                            Country = p.Country,
                            PinCode = p.Pincode,
                            RegistrationDate = p.RegisteredAt
                        }).ToListAsync(cancellationToken);

                    response.PatientsData = patients;
                    response.Success = true;
                    response.Message = patients is not null ? "Patients retrieved successfully." : "No patients found for the hospital";
                }
                else
                {
                    response.Message = "Hospital does not exist.";
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
