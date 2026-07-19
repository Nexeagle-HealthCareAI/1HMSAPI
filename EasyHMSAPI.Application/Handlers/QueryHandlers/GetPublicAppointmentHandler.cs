using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Guest "my booking" lookup — the ONLY gate is knowing the AppointmentId (a GUID, effectively
    // unguessable), same trust model as an e-commerce guest order-lookup page. That's why the
    // response is deliberately minimal (see PublicAppointmentSummary) — anyone with the ID can
    // read this, so it must never include the patient's name, mobile, or reason for visit.
    public class GetPublicAppointmentHandler : IRequestHandler<GetPublicAppointmentRequestModel, GetPublicAppointmentResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicAppointmentHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicAppointmentResponseModel> Handle(GetPublicAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var appt = await _context.Appointments
                .Where(a => a.ApptId == request.AppointmentId)
                .Select(a => new { a.ApptId, a.HospitalId, a.DoctorId, a.ApptDate, a.StartAt, a.CurrentStatusCode })
                .FirstOrDefaultAsync(cancellationToken);

            if (appt == null)
            {
                return new GetPublicAppointmentResponseModel { Success = false, Message = "Appointment not found." };
            }

            var hospitalName = await _context.Hospitals
                .Where(h => h.HospitalID == appt.HospitalId)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken);
            var doctorName = await _context.Doctors
                .Where(d => d.DoctorID == appt.DoctorId)
                .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            return new GetPublicAppointmentResponseModel
            {
                Success = true,
                Appointment = new PublicAppointmentSummary
                {
                    AppointmentId = appt.ApptId,
                    DoctorName = doctorName ?? "Doctor",
                    HospitalName = hospitalName ?? "Hospital",
                    ApptDate = appt.ApptDate,
                    StartAt = appt.StartAt,
                    Status = PublicAppointmentStatusLabels.ToPatientLabel(appt.CurrentStatusCode),
                    StatusCode = appt.CurrentStatusCode ?? string.Empty,
                },
            };
        }
    }
}
