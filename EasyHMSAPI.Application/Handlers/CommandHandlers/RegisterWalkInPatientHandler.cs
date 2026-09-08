using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RegisterWalkInPatientHandler : IRequestHandler<RegisterWalkInPatientRequestModel, RegisterWalkInPatientResponseModel>
    {
        private readonly AppDbContext _context;

        public RegisterWalkInPatientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RegisterWalkInPatientResponseModel> Handle(RegisterWalkInPatientRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var patient = await AppointmentBookingHelpers.FindOrCreatePatientAsync(
                    _context, request.Patient, request.HospitalId, request.UserId, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                return new RegisterWalkInPatientResponseModel
                {
                    Success = true,
                    Message = "Patient registered successfully.",
                    PatientId = patient.PatientId,
                    FullName = patient.FullName,
                    Mobile = patient.Mobile,
                    Age = patient.Age,
                    Sex = patient.Sex,
                };
            }
            catch (ArgumentException ex)
            {
                return new RegisterWalkInPatientResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
